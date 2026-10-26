using System;
using System.Collections.Generic;
using System.Text.Json;
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

        public MemoryExtractor(IBrainModel model) { _model = model; }

        public async Task<List<MemoryItem>> ExtractAsync(string transcript, string sourceAuditId, CancellationToken ct)
        {
            var result = new List<MemoryItem>();
            if (string.IsNullOrWhiteSpace(transcript)) return result;
            string system = "你只做记忆提取。输出 JSON 数组，0-3 条；没有值得记的就输出 []。每条 {\"kind\":\"semantic|episodic\",\"key\":\"短键\",\"value\":\"事实/偏好/经历/承诺\",\"summary\":\"一句摘要\"}。不要寒暄，不要推测。";
            try
            {
                string raw = await _model.CompleteAsync(system, transcript, ct).ConfigureAwait(false);
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
            return result;
        }
    }
}
