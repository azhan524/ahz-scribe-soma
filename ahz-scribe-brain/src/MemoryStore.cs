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
    /// Brain-side memory store. It never touches the workspace; it only owns its own JSONL file.
    /// </summary>
    public sealed class MemoryStore : IMemoryStore
    {
        static readonly Regex Word = new Regex(@"[\u4e00-\u9fff]{2,}|[A-Za-z0-9_]{2,}", RegexOptions.Compiled);
        readonly string _path;
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
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string line = JsonSerializer.Serialize(item, _json);
                using (var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    await w.WriteLineAsync(line).ConfigureAwait(false);
                }
                return item.Id;
            }
            finally { _gate.Release(); }
        }

        public async Task<List<MemoryItem>> LoadAllAsync(CancellationToken ct)
        {
            var list = new List<MemoryItem>();
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!File.Exists(_path)) return list;
                foreach (string line in File.ReadLines(_path))
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        MemoryItem m = JsonSerializer.Deserialize<MemoryItem>(line, _json);
                        if (m != null) list.Add(m);
                    }
                    catch { }
                }
                return list;
            }
            finally { _gate.Release(); }
        }

        public async Task<List<MemoryItem>> RetrieveAsync(string query, int topK, CancellationToken ct)
        {
            List<MemoryItem> all = await LoadAllAsync(ct).ConfigureAwait(false);
            if (topK <= 0) topK = 5;
            HashSet<string> words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTokens(words, query);

            var scored = new List<KeyValuePair<MemoryItem, int>>();
            foreach (MemoryItem m in all)
            {
                if (m == null || m.Status == "deleted") continue;
                string text = (m.Key ?? "") + " " + (m.Value ?? "") + " " + (m.Summary ?? "");
                var mt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddTokens(mt, text);
                int score = 0;
                foreach (string w in mt) if (words.Contains(w)) score++;
                if (score > 0) scored.Add(new KeyValuePair<MemoryItem, int>(m, score));
            }
            return scored.OrderByDescending(x => x.Value)
                .ThenByDescending(x => x.Key.Ts, StringComparer.Ordinal)
                .Take(topK).Select(x => x.Key).ToList();
        }

        static void AddTokens(HashSet<string> set, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (Match m in Word.Matches(text))
            {
                string s = m.Value;
                set.Add(s);
                // Chinese runs: add 2-char shingles so "我喜欢什么" can hit "用户喜欢蓝鲸".
                if (s.Length >= 4)
                    for (int i = 0; i + 2 <= s.Length; i++) set.Add(s.Substring(i, 2));
            }
        }
        public void Dispose() { _gate.Dispose(); }
    }
}
