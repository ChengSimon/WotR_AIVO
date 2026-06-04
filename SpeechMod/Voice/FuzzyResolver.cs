using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.Sound;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AiVoiceoverMod.Voice;

// ----------------------------
// Source localization format (WOTR: Wrath_Data/StreamingAssets/Localization/<locale>.json):
// {
//   "$id": "1",                  <- Json.NET reference id at the root; ignored on read
//   "strings": {
//     "<GUID>": "text",          <- value is the string directly
//     ...
//   }
// }
// Kept in sync with JsonPreproc/Program.cs (the offline preprocessor).
// ----------------------------
public sealed class SourceRoot
{
    [JsonProperty("strings")]
    public Dictionary<string, string> strings { get; set; } = new();
}

// ----------------------------
// Precompiled index format (stored as compact JSON inside cache/<locale>.idxv1):
// {
//   "k": 64,
//   "seeds": [u32, ...],
//   "entries": [
//     { "id": "<GUID>", "text": "...", "sig": [u32, ...] },
//     ...
//   ]
// }
// ----------------------------
public sealed class PrecompiledDb
{
    public int k { get; set; }
    public uint[] seeds { get; set; } = Array.Empty<uint>();
    public List<DbEntry> entries { get; set; } = new();
}

public sealed class DbEntry
{
    public string id { get; set; } = "";     // GUID from the localization file
    public string text { get; set; } = "";   // glossary-stripped text
    public uint[] sig { get; set; } = Array.Empty<uint>(); // MinHash signature
}

// ----------------------------
// MinHash over char 3-grams
// ----------------------------
public sealed class MinHasher
{
    private readonly uint[] _seeds;
    public int K => _seeds.Length;

    public MinHasher(uint[] seeds) => _seeds = seeds;

    public static uint[] MakeRandomSeeds(int k, int seed = 1337)
    {
        var s = new uint[k];
        var rng = new System.Random(seed);
        for (int i = 0; i < k; i++)
            s[i] = (uint)rng.Next(int.MinValue, int.MaxValue) | 1u;
        return s;
    }

    public uint[] Signature(string s)
    {
        var sig = new uint[_seeds.Length];
        for (int i = 0; i < sig.Length; i++) sig[i] = uint.MaxValue;
        if (string.IsNullOrEmpty(s)) return sig;

        for (int i = 0; i + 3 <= s.Length; i++)
        {
            uint h = Fnv1a32(s, i, 3);
            for (int k = 0; k < _seeds.Length; k++)
            {
                uint mixed = Mix32(h ^ _seeds[k]);
                if (mixed < sig[k]) sig[k] = mixed;
            }
        }
        return sig;
    }

    public static float Similarity(uint[] a, uint[] b)
    {
        int len = a.Length;
        int eq = 0;
        for (int i = 0; i < len; i++) if (a[i] == b[i]) eq++;
        return len == 0 ? 1f : (float)eq / len;
    }

    private static uint Fnv1a32(string s, int start, int length)
    {
        const uint FNV_OFFSET = 2166136261;
        const uint FNV_PRIME = 16777619;
        uint hash = FNV_OFFSET;
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            unchecked
            {
                ushort u = s[i];
                byte b0 = (byte)(u & 0xFF);
                byte b1 = (byte)(u >> 8);
                hash ^= b0; hash *= FNV_PRIME;
                hash ^= b1; hash *= FNV_PRIME;
            }
        }
        return hash;
    }

    private static uint Mix32(uint x)
    {
        unchecked
        {
            x ^= x >> 16; x *= 0x7feb352d;
            x ^= x >> 15; x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }
    }
}

public static class NGram
{
    public static float Jaccard(string a, string b)
    {
        var A = HashSetPool.Shared.Rent();
        var B = HashSetPool.Shared.Rent();
        Fill3GramHashes(a, A);
        Fill3GramHashes(b, B);
        int inter = 0;
        if (A.Count <= B.Count)
        {
            foreach (var x in A) if (B.Contains(x)) inter++;
        }
        else
        {
            foreach (var x in B) if (A.Contains(x)) inter++;
        }
        int uni = A.Count + B.Count - inter;
        HashSetPool.Shared.Return(A); HashSetPool.Shared.Return(B);
        return uni == 0 ? 1f : (float)inter / uni;
    }

    private static void Fill3GramHashes(string s, HashSet<uint> dest)
    {
        dest.Clear();
        if (string.IsNullOrEmpty(s)) return;
        for (int i = 0; i + 3 <= s.Length; i++)
            dest.Add(Fnv1a32(s, i, 3));
    }

    private static uint Fnv1a32(string s, int start, int length)
    {
        const uint FNV_OFFSET = 2166136261;
        const uint FNV_PRIME = 16777619;
        uint hash = FNV_OFFSET;
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            unchecked
            {
                ushort u = s[i];
                byte b0 = (byte)(u & 0xFF);
                byte b1 = (byte)(u >> 8);
                hash ^= b0; hash *= FNV_PRIME;
                hash ^= b1; hash *= FNV_PRIME;
            }
        }
        return hash;
    }

    private sealed class HashSetPool
    {
        public static readonly HashSetPool Shared = new HashSetPool();
        private readonly Stack<HashSet<uint>> _pool = new Stack<HashSet<uint>>();
        public HashSet<uint> Rent() { return _pool.Count > 0 ? _pool.Pop() : new HashSet<uint>(); }
        public void Return(HashSet<uint> set) { set.Clear(); _pool.Push(set); }
    }
}


// ----------------------------
// Resolver (returns GUID, text, scores)
// ----------------------------
public sealed class FuzzyResolver
{
    private const string DefaultLocale = "enGB";
    private const int DefaultSignatureSize = 64;
    private const int DefaultSeed = 1337;

    // Removes glossary markup, keeping the visible text: "{g|Encyclopedia:DC}DC{/g}" -> "DC".
    // Must stay in sync with both the index builder and the runtime query cleaning below.
    private static readonly Regex s_GlossaryRegex = new(@"\{g\|[^}]*\}|\{/g\}", RegexOptions.Compiled);
    private static readonly Regex s_TagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public static string s_ModDirectory;
    public static FuzzyResolver Singleton;

    // Locale whose index is currently loaded into Singleton, so we can detect changes.
    private static string s_LoadedLocale;

    public static bool ResolveAndPlay(string text, string kind, GameObject obj)
    {
        EnsureDatabaseForCurrentLocale();

        if (Singleton == null)
        {
            Debug.LogWarning("AIVO fuzzy resolver has no index loaded.");
            return false;
        }

        var mcName = Game.Instance?.Player?.MainCharacter.Value?.CharacterName;
        if (mcName != null)
        {
            // A bit hacky, but improves matching performance a lot!
            text = text.Replace(mcName, "{name}");
        }
        // Strip XML tags and glossary markup ({g|Encyclopedia:DC}DC{/g} -> DC), keeping the visible text.
        var cleanText = s_TagRegex.Replace(text, "");
        cleanText = s_GlossaryRegex.Replace(cleanText, "");
        cleanText = cleanText.Trim();

        ResolveResult res = Singleton.Query(cleanText);
#if DEBUG
        Debug.Log($"{kind} (FUZZY): {res.Best.Id}");
#endif
        SoundEventsManager.PostEvent("evt_" + res.Best.Id, obj);
        return false;
    }

    // Builds/loads the index for the current locale if it isn't already loaded. Cheap no-op when unchanged,
    // so it is safe to call from the Init/OnLocaleChanged hooks and as a backstop on every resolve.
    public static void EnsureDatabaseForCurrentLocale()
    {
        var locale = GetCurrentLocaleName();
        if (Singleton != null && string.Equals(s_LoadedLocale, locale, StringComparison.OrdinalIgnoreCase))
            return;
        LoadForLocale(locale);
    }

    private static string GetCurrentLocaleName()
    {
        try
        {
            if (LocalizationManager.Initialized)
                return LocalizationManager.CurrentLocale.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"AIVO could not read current locale: {ex.Message}");
        }
        return DefaultLocale;
    }

    private static void LoadForLocale(string locale)
    {
        EnsureModDirectory();
        if (string.IsNullOrWhiteSpace(locale)) locale = DefaultLocale;

        UnityEngine.Debug.Log($"AIVO loading index for locale {locale}...");
        try
        {
            var indexFile = EnsureIndex(locale);

            if (string.IsNullOrEmpty(indexFile) || !File.Exists(indexFile))
            {
                if (!string.Equals(locale, DefaultLocale, StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Debug.LogWarning($"AIVO index unavailable for {locale}; falling back to {DefaultLocale}.");
                    LoadForLocale(DefaultLocale);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"AIVO index unavailable for {DefaultLocale}.");
                }
                return;
            }

            var json = File.ReadAllText(indexFile, Encoding.UTF8);
            var db = JsonConvert.DeserializeObject<PrecompiledDb>(json);

            if (db != null)
            {
                Singleton = new FuzzyResolver(db);
                s_LoadedLocale = locale;
                UnityEngine.Debug.Log($"AIVO loaded {db.entries.Count} entries from {Path.GetFileName(indexFile)}.");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            UnityEngine.Debug.LogWarning($"AIVO failed to load index for {locale}!");
        }
    }

    // Ensures cache/<locale>.idxv1 exists and is newer than the game's localization file, (re)building it when
    // missing or stale. Returns the index path, or a stale cache / null when the source file cannot be found.
    private static string EnsureIndex(string locale)
    {
        var indexFile = GetIndexPath(locale);

        // The localization JSON the game itself loads: Application.streamingAssetsPath is exactly what
        // LocalizationManager reads through Owlcat's ApplicationPaths.streamingAssetsPath (it just caches
        // Application.streamingAssetsPath at startup), so this always points at the file the game uses.
        var sourceFile = string.IsNullOrEmpty(Application.streamingAssetsPath)
            ? null
            : Path.Combine(Application.streamingAssetsPath, "Localization", locale + ".json");

        if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
        {
            // No source to build from: fall back to a previously generated index if present.
            UnityEngine.Debug.LogWarning($"AIVO could not find {locale}.json under StreamingAssets/Localization.");
            return File.Exists(indexFile) ? indexFile : null;
        }

        if (File.Exists(indexFile) && File.GetLastWriteTimeUtc(indexFile) >= File.GetLastWriteTimeUtc(sourceFile))
        {
            return indexFile;
        }

        UnityEngine.Debug.Log($"AIVO building {Path.GetFileName(indexFile)} from {sourceFile}...");
        var db = BuildIndex(sourceFile, DefaultSignatureSize, DefaultSeed);
        Directory.CreateDirectory(Path.GetDirectoryName(indexFile));
        File.WriteAllText(indexFile, JsonConvert.SerializeObject(db, Formatting.None), Encoding.UTF8);
        UnityEngine.Debug.Log($"AIVO wrote {db.entries.Count} entries to {indexFile}.");
        return indexFile;
    }

    private static PrecompiledDb BuildIndex(string inputPath, int k, int seed)
    {
        var src = JsonConvert.DeserializeObject<SourceRoot>(File.ReadAllText(inputPath, Encoding.UTF8));

        if (src?.strings == null || src.strings.Count == 0)
            throw new InvalidDataException($"Localization file has no strings: {inputPath}");

        var seeds = MinHasher.MakeRandomSeeds(k, seed);
        var mh = new MinHasher(seeds);
        var entries = new List<DbEntry>(src.strings.Count);

        foreach (var pair in src.strings)
        {
            // Strip glossary markup so the DB text/signature match the runtime query cleaning above.
            var text = StripGlossary(pair.Value ?? "");
            entries.Add(new DbEntry
            {
                id = pair.Key,
                text = text,
                sig = mh.Signature(text)
            });
        }

        return new PrecompiledDb { k = k, seeds = seeds, entries = entries };
    }

    private static string StripGlossary(string s) => string.IsNullOrEmpty(s) ? s : s_GlossaryRegex.Replace(s, "");

    private static string GetIndexPath(string locale)
    {
        EnsureModDirectory();
        return Path.Combine(s_ModDirectory, "cache", locale + ".idxv1");
    }

    private static void EnsureModDirectory()
    {
        if (!string.IsNullOrEmpty(s_ModDirectory)) return;
        s_ModDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    private readonly MinHasher _mh;
    private readonly List<DbEntry> _entries;

    public FuzzyResolver(PrecompiledDb db)
    {
        _mh = new MinHasher(db.seeds);
        _entries = db.entries;
    }

    public ResolveResult Query(string input, int topK = 10, bool refine = true)
    {
        var qsig = _mh.Signature(input);

        // Candidate heap (small K) by MinHash estimate
        var heap = new (int idx, float sim)[topK];
        int filled = 0;

        for (int i = 0; i < _entries.Count; i++)
        {
            float s = MinHasher.Similarity(qsig, _entries[i].sig);
            if (filled < topK)
            {
                heap[filled++] = (i, s);
                if (filled == topK) Array.Sort(heap, (a, b) => a.sim.CompareTo(b.sim));
            }
            else if (s > heap[0].sim)
            {
                heap[0] = (i, s);
                Array.Sort(heap, (a, b) => a.sim.CompareTo(b.sim));
            }
        }

        var cand = heap.Take(filled).Select(x => (idx: x.idx, score: x.sim)).OrderByDescending(x => x.score).ToList();

        if (refine)
        {
            for (int i = 0; i < cand.Count; i++)
            {
                float j = NGram.Jaccard(input, _entries[cand[i].idx].text);
                cand[i] = (cand[i].idx, j);
            }
            cand.Sort((a, b) => b.score.CompareTo(a.score));
        }

        var best = cand[0];
        var bestEntry = _entries[best.idx];

        return new ResolveResult
        {
            Best = new ResolveHit { Id = bestEntry.id, Text = bestEntry.text, Score = best.score },
            Candidates = cand.Select(c => new ResolveHit { Id = _entries[c.idx].id, Text = _entries[c.idx].text, Score = c.score }).ToList()
        };
    }
}

public sealed class ResolveResult
{
    public ResolveHit Best { get; set; } = new();
    public List<ResolveHit> Candidates { get; set; } = new();
}

public sealed class ResolveHit
{
    public string Id { get; set; } = "";    // GUID
    public string Text { get; set; } = "";
    public float Score { get; set; }
}
