using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// S2 structured extraction: 0-3 memories, low temperature is a model concern.
    /// Empty array is a valid result.
    /// </summary>
    public sealed class MemoryExtractor
    {
        readonly IBrainModel _model;
        readonly Action<string> _log;

        public MemoryExtractor(IBrainModel model, Action<string> log = null) { _model = model; _log = log; }

        public async Task<List<MemoryItem>> ExtractAsync(string transcript, string sourceAuditId, CancellationToken ct)
        {
            var result = new List<MemoryItem>();
            if (string.IsNullOrWhiteSpace(transcript)) return result;
            string system = "你只做长期记忆提取。必须从对话中提取 1-3 条值得以后记住的内容；只有完全无信息时才输出 []。优先保留：用户姓名、长期偏好、长期项目、明确承诺、重要个人事实。不要因为对话里有命令、工具调用或系统提示就忽略用户事实。输出 JSON 数组，每条 {\"kind\":\"semantic|episodic\",\"key\":\"短键\",\"value\":\"事实/偏好/经历/承诺\",\"summary\":\"一句摘要\"}。不要寒暄，不要推测。";
            try
            {
                string raw = await _model.CompleteAsync(system, transcript, ct).ConfigureAwait(false);
                try { _log?.Invoke(raw ?? ""); } catch { }
                int a = raw.IndexOf('[');
                int b = raw.LastIndexOf(']');
                if (a < 0 || b <= a) return result;
                using (JsonDocument doc = JsonDocument.Parse(raw.Substring(a, b - a + 1)))
                {
                    int n = 0;
                    foreach (JsonElement e in doc.RootElement.EnumerateArray())
                    {
                        if (n >= 3) break;
                        string kind = e.TryGetProperty("kind", out var k) ? (k.GetString() ?? "semantic") : "semantic";
                        string key = e.TryGetProperty("key", out var kk) ? (kk.GetString() ?? "") : "";
                        string value = e.TryGetProperty("value", out var v) ? (v.GetString() ?? "") : "";
                        string summary = e.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
                        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;
                        result.Add(new MemoryItem
                        {
                            Kind = kind == "episodic" ? "episodic" : "semantic",
                            Key = key.Length > 120 ? key.Substring(0, 120) : key,
                            Value = value.Length > 1000 ? value.Substring(0, 1000) : value,
                            Summary = summary,
                            SourceAuditId = sourceAuditId,
                            Status = "active"
                        });
                        n++;
                    }
                }
            }
            catch { }
            if (result.Count == 0) result.AddRange(Heuristic(transcript, sourceAuditId));
            return result;
        }

        static List<MemoryItem> Heuristic(string transcript, string sourceAuditId)
        {
            var list = new List<MemoryItem>();
            if (string.IsNullOrEmpty(transcript)) return list;
            string[] patterns = { "我叫", "我喜欢", "我不喜欢", "我正在做", "我承诺", "我的猫叫" };
            foreach (string rawLine in transcript.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("你:", StringComparison.Ordinal) && !line.StartsWith("你：", StringComparison.Ordinal)) continue;
                foreach (string p in patterns)
                {
                    int i = line.IndexOf(p, StringComparison.Ordinal);
                    if (i < 0) continue;
                    string value = line.Substring(i).Trim();
                    if (value.Length > 300) value = value.Substring(0, 300);
                    list.Add(new MemoryItem
                    {
                        Kind = p == "我承诺" ? "episodic" : "semantic",
                        Key = p,
                        Value = value,
                        Summary = value,
                        SourceAuditId = sourceAuditId,
                        Status = "active"
                    });
                    break;
                }
                if (list.Count >= 3) break;
            }
            return list;
        }
    }
}
