using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

// ----------------------------
// Models
// ----------------------------

// Input format (WOTR: Wrath_Data/StreamingAssets/Localization/enGB.json):
// {
//   "$id": "1",                       <- Json.NET reference id at the root; ignored on read
//   "strings": {
//     "<GUID>": "text",               <- value is the string directly (RT wrapped it in { Offset, Text })
//     ...
//   }
// }
public sealed class SourceRoot
{
    public Dictionary<string, string> strings { get; set; } = new();
}

// Precompiled format we write/read:
//
// {
//   "k": 64,
//   "seeds": [u32, ...],
//   "entries": [
//     { "id": "<GUID>", "text": "...", "sig": [u32, ...] },
//     ...
//   ]
// }
public sealed class PrecompiledDb
{
    public int k { get; set; }
    public uint[] seeds { get; set; } = Array.Empty<uint>();
    public List<DbEntry> entries { get; set; } = new();
}
public sealed class DbEntry
{
    public string id { get; set; } = "";     // GUID from your input
    public string text { get; set; } = "";   // original text
    public uint[] sig { get; set; } = Array.Empty<uint>(); // MinHash signature
}

// ----------------------------
// MinHash over char 3-grams (no normalization)
// ----------------------------
public sealed class MinHasher
{
    private readonly uint[] _seeds;
    public int K => _seeds.Length;

    public MinHasher(uint[] seeds) => _seeds = seeds;

    public static uint[] MakeRandomSeeds(int k, int seed = 1337)
    {
        var s = new uint[k];
        var rng = new Random(seed);
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
            uint h = Fnv1a32(s.AsSpan(i, 3));
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

    private static uint Fnv1a32(ReadOnlySpan<char> span)
    {
        const uint FNV_OFFSET = 2166136261;
        const uint FNV_PRIME = 16777619;
        uint hash = FNV_OFFSET;
        for (int i = 0; i < span.Length; i++)
        {
            unchecked
            {
                ushort u = span[i];
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

// ----------------------------
// Exact char 3-gram Jaccard (no normalization)
// ----------------------------
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
            dest.Add(Fnv1a32(s.AsSpan(i, 3)));
    }

    private static uint Fnv1a32(ReadOnlySpan<char> span)
    {
        const uint FNV_OFFSET = 2166136261;
        const uint FNV_PRIME = 16777619;
        uint hash = FNV_OFFSET;
        for (int i = 0; i < span.Length; i++)
        {
            unchecked
            {
                ushort u = span[i];
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
        public static readonly HashSetPool Shared = new();
        private readonly Stack<HashSet<uint>> _pool = new();
        public HashSet<uint> Rent() => _pool.Count > 0 ? _pool.Pop() : new HashSet<uint>();
        public void Return(HashSet<uint> set) { set.Clear(); _pool.Push(set); }
    }
}

// ----------------------------
// Resolver (returns GUID, text, scores)
// ----------------------------
public sealed class FuzzyResolver
{
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

// ----------------------------
// CLI: preprocess | resolve
// ----------------------------
public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  preprocess <input_strings.json> <output_db.json> [k=64] [seed=1337]");
            Console.WriteLine("  resolve    <db.json> <query> [topK=10]");
            return;
        }

        var cmd = args[0].ToLowerInvariant();
        if (cmd == "preprocess")
        {
            var input = args[1];
            var output = args[2];
            int k = args.Length >= 4 && int.TryParse(args[3], out var kk) ? kk : 64;
            int seed = args.Length >= 5 && int.TryParse(args[4], out var sd) ? sd : 1337;

            Preprocess(input, output, k, seed);
            Console.WriteLine($"Wrote precompiled DB → {output}");
        }
        else if (cmd == "resolve")
        {
            var dbPath = args[1];
            var query = args[2];
            int topK = args.Length >= 4 && int.TryParse(args[3], out var tk) ? tk : 10;

            var db = JsonSerializer.Deserialize<PrecompiledDb>(File.ReadAllText(dbPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var resolver = new FuzzyResolver(db);
            var res = resolver.Query(query, topK: topK, refine: true);

            Console.WriteLine($"BEST GUID={res.Best.Id}");
            Console.WriteLine($"SCORE={res.Best.Score:0.000}");
            Console.WriteLine(res.Best.Text);
        }
        else
        {
            Console.WriteLine("Unknown command. Use 'preprocess' or 'resolve'.");
        }
    }

    private static void Preprocess(string inputPath, string outputPath, int k, int seed)
    {
        var src = JsonSerializer.Deserialize<SourceRoot>(File.ReadAllText(inputPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var seeds = MinHasher.MakeRandomSeeds(k, seed);
        var mh = new MinHasher(seeds);

        var entries = new List<DbEntry>(src.strings.Count);
        foreach (var (guid, value) in src.strings)
        {
            // Strip glossary markup so the DB text/signature match the runtime query cleaning in FuzzyResolver.
            var text = StripGlossary(value ?? "");
            entries.Add(new DbEntry
            {
                id = guid,
                text = text,
                sig = mh.Signature(text)
            });
        }

        var outObj = new PrecompiledDb
        {
            k = k,
            seeds = seeds,
            entries = entries
        };

        var json = JsonSerializer.Serialize(outObj, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(outputPath, json);
    }

    // Removes glossary markup, keeping the visible text: "{g|Encyclopedia:DC}DC{/g}" -> "DC".
    // Must stay in sync with the same strip applied to runtime queries in FuzzyResolver.ResolveAndPlay.
    private static readonly Regex s_GlossaryRegex = new(@"\{g\|[^}]*\}|\{/g\}", RegexOptions.Compiled);

    private static string StripGlossary(string s) => string.IsNullOrEmpty(s) ? s : s_GlossaryRegex.Replace(s, "");
}
