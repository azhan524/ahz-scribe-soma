using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// Brain-side memory store. It never touches the workspace; it only owns its own JSONL files.
    /// S2: semantic / episodic / disputed are separate append-only files; the legacy
    /// memory.jsonl remains readable for migration.
    /// </summary>
    public sealed class MemoryStore : IMemoryStore
    {
        static readonly Regex Word = new Regex(@"[\u4e00-\u9fff]{2,}|[A-Za-z0-9_]{2,}", RegexOptions.Compiled);
        readonly string _path;
        readonly string _semantic;
        readonly string _episodic;
        readonly string _disputed;
        readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public MemoryStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is required");
            _path = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(_path);
            _semantic = Path.Combine(dir, "semantic.jsonl");
            _episodic = Path.Combine(dir, "episodic.jsonl");
            _disputed = Path.Combine(dir, "disputed.jsonl");
        }

        public async Task<string> RememberAsync(MemoryItem item, CancellationToken ct)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrEmpty(item.Id)) item.Id = "mem_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            if (string.IsNullOrEmpty(item.Ts)) item.Ts = DateTimeOffset.UtcNow.ToString("o");
            if (string.IsNullOrEmpty(item.Status)) item.Status = "active";

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                List<MemoryItem> all = ReadAllUnsafe();
                MemoryItem sameKey = all.FirstOrDefault(m => m.Status == "active" && !string.IsNullOrEmpty(item.Key) && m.Key == item.Key);
                if (sameKey != null)
                {
                    if (sameKey.Value == item.Value)
                    {
                        DateTimeOffset oldTs;
                        if (DateTimeOffset.TryParse(sameKey.Ts, out oldTs) && (DateTimeOffset.UtcNow - oldTs).TotalHours < 24)
                            return sameKey.Id;
                    }
                    else
                    {
                        item.Status = "disputed";
                        item.DisputedWith = sameKey.Id;
                    }
                }

                string path = PathFor(item);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string line = JsonSerializer.Serialize(item, _json);
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
                    await w.WriteLineAsync(line).ConfigureAwait(false);
                return item.Id;
            }
            finally { _gate.Release(); }

            string PathFor(MemoryItem m)
            {
                string k = (m.Kind ?? "").ToLowerInvariant();
                if (m.Status == "disputed" || k == "disputed") return _disputed;
                if (k == "semantic" || k == "fact" || k == "preference") return _semantic;
                if (k == "episodic" || k == "episode") return _episodic;
                return _path;
            }
        }

        public async Task<List<MemoryItem>> LoadAllAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try { return ReadAllUnsafe(); }
            finally { _gate.Release(); }
        }

        List<MemoryItem> ReadAllUnsafe()
        {
            var list = new List<MemoryItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in new[] { _path, _semantic, _episodic, _disputed })
            {
                if (!File.Exists(path)) continue;
                foreach (string line in File.ReadLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        MemoryItem m = JsonSerializer.Deserialize<MemoryItem>(line, _json);
                        if (m != null && seen.Add(m.Id ?? Guid.NewGuid().ToString("N"))) list.Add(m);
                    }
                    catch { }
                }
            }
            return list;
        }

        public async Task<List<MemoryItem>> RetrieveAsync(string query, int topK, CancellationToken ct)
        {
            List<MemoryItem> all = await LoadAllAsync(ct).ConfigureAwait(false);
            if (topK <= 0) topK = 5;
            HashSet<string> words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTokens(words, query);

            var scored = new List<KeyValuePair<MemoryItem, double>>();
            foreach (MemoryItem m in all)
            {
                if (m == null || m.Status == "deleted") continue;
                string text = (m.Key ?? "") + " " + (m.Value ?? "") + " " + (m.Summary ?? "");
                var mt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddTokens(mt, text);
                int score = 0;
                foreach (string w in mt) if (words.Contains(w)) score++;
                if (score == 0) continue;
                double days = 30;
                DateTimeOffset ts;
                if (DateTimeOffset.TryParse(m.Ts, out ts)) days = Math.Max(0, (DateTimeOffset.UtcNow - ts).TotalDays);
                double decay = 1.0 / (1.0 + days / 30.0);
                scored.Add(new KeyValuePair<MemoryItem, double>(m, score * 10.0 + decay));
            }
            return scored.OrderByDescending(x => x.Value)
                .Take(topK).Select(x => x.Key).ToList();
        }

        static void AddTokens(HashSet<string> set, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (Match m in Word.Matches(text))
            {
                string s = m.Value;
                set.Add(s);
                if (s.Length >= 4)
                    for (int i = 0; i + 2 <= s.Length; i++) set.Add(s.Substring(i, 2));
            }
        }
        public void Dispose() { _gate.Dispose(); }
    }
}
