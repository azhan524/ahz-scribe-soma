using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AhzScribe.Brain;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    public sealed class FacePulseBody : IBodyStatusSource
    {
        public DateTime LastUserAt = DateTime.UtcNow;

        public BodyStatus Snapshot()
        {
            return new BodyStatus
            {
                IdleSeconds = Math.Max(0, (int)(DateTime.UtcNow - LastUserAt).TotalSeconds)
            };
        }
    }

    public sealed class FacePulseAudit : IAuditSink
    {
        readonly Action<string> _log;

        public FacePulseAudit(Action<string> log) { _log = log; }

        public Task<string> AppendAsync(string span, string tool, string decision, string result,
            string tier, Dictionary<string, object> args, string reason, CancellationToken ct)
        {
            try { _log?.Invoke("[pulse] " + span + " / " + tool + " / " + decision + " / " + reason); }
            catch { }
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }
    }

    public sealed class FacePulseSink : IPulseSink
    {
        readonly Action<PulseEvent> _emit;

        public FacePulseSink(Action<PulseEvent> emit) { _emit = emit; }

        public Task EmitAsync(PulseEvent evt, CancellationToken ct)
        {
            _emit?.Invoke(evt);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Model-assisted pulse. It only runs when the user has been idle for a while
    /// and at most every 30 minutes. The model may return none; it has no tools.
    /// </summary>
    public sealed class ModelPulseDecider : IPulseDecider
    {
        readonly IMemoryStore _memory;
        readonly IBrainModel _model;
        readonly IntentPulseDecider _intents;
        DateTime _lastModelCall = DateTime.MinValue;

        public ModelPulseDecider(IMemoryStore memory, IBrainModel model)
        {
            _memory = memory;
            _model = model;
            _intents = new IntentPulseDecider(memory);
        }

        public async Task<PulseDecision> DecideAsync(BodyStatus body, List<MemoryItem> memory, CancellationToken ct)
        {
            PulseDecision intent = await _intents.DecideAsync(body, memory, ct).ConfigureAwait(false);
            if (intent != null && intent.Action == "message") return intent;

            int hour = DateTime.Now.Hour;
            if (hour < 8 || hour >= 23) return intent ?? new PulseDecision { Action = "none", Reason = "quiet_hours" };
            if (body != null && body.ActiveTask) return new PulseDecision { Action = "none", Reason = "active_task" };
            if (body != null && body.IdleSeconds < 600) return new PulseDecision { Action = "none", Reason = "not_idle" };
            if ((DateTime.UtcNow - _lastModelCall).TotalMinutes < 30) return new PulseDecision { Action = "none", Reason = "model_cooldown" };

            _lastModelCall = DateTime.UtcNow;
            try
            {
                List<MemoryItem> all = memory ?? await _memory.LoadAllAsync(ct).ConfigureAwait(false);
                var sb = new System.Text.StringBuilder();
                foreach (MemoryItem m in all.Skip(Math.Max(0, all.Count - 12)))
                    sb.AppendLine("- " + (m.Kind ?? "") + " / " + (m.Key ?? "") + " = " + (m.Value ?? ""));
                string system = "你是 Ahz Scribe 的脉冲决策器。行动空间只有 none 和 message。没有明确、必要、不打扰的理由就返回 none；不要寒暄，不要为了说话而说话。只输出 JSON：{\"action\":\"none|message\",\"text\":\"\",\"reason\":\"\"}。";
                string user = "当前空闲秒数：" + (body == null ? 0 : body.IdleSeconds) + "\n最近记忆：\n" + sb.ToString();
                string raw = await _model.CompleteAsync(system, user, ct).ConfigureAwait(false);
                int a = raw.IndexOf('{');
                int b = raw.LastIndexOf('}');
                if (a < 0 || b <= a) return new PulseDecision { Action = "none", Reason = "unparseable" };
                using (JsonDocument doc = JsonDocument.Parse(raw.Substring(a, b - a + 1)))
                {
                    JsonElement root = doc.RootElement;
                    string action = root.TryGetProperty("action", out JsonElement ar) ? (ar.GetString() ?? "none") : "none";
                    string text = root.TryGetProperty("text", out JsonElement tr) ? (tr.GetString() ?? "") : "";
                    string reason = root.TryGetProperty("reason", out JsonElement rr) ? (rr.GetString() ?? "") : "";
                    if (!string.Equals(action, "message", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(text))
                        return new PulseDecision { Action = "none", Reason = reason.Length == 0 ? "model_none" : reason };
                    if (text.Length > 300) text = text.Substring(0, 300);
                    return new PulseDecision { Action = "message", Text = text, Reason = "model:" + reason };
                }
            }
            catch (Exception ex)
            {
                return new PulseDecision { Action = "none", Reason = "model_error:" + ex.Message };
            }
        }
    }

    /// <summary>
    /// Conservative pulse: only user-created intents can produce a message.
    /// No intent -> none. Quiet hours are enforced here, not by the model.
    /// </summary>
    public sealed class IntentPulseDecider : IPulseDecider
    {
        readonly IMemoryStore _memory;

        public IntentPulseDecider(IMemoryStore memory) { _memory = memory; }

        public async Task<PulseDecision> DecideAsync(BodyStatus body, List<MemoryItem> memory, CancellationToken ct)
        {
            int hour = DateTime.Now.Hour;
            if (hour < 8 || hour >= 23) return new PulseDecision { Action = "none", Reason = "quiet_hours" };

            List<MemoryItem> all = memory ?? await _memory.LoadAllAsync(ct).ConfigureAwait(false);
            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MemoryItem m in all)
                if (string.Equals(m.Kind, "intent_done", StringComparison.OrdinalIgnoreCase)) done.Add(m.Key ?? "");

            MemoryItem intent = all.FirstOrDefault(m =>
                string.Equals(m.Kind, "intent", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(m.Value) &&
                !done.Contains(m.Key ?? ""));
            if (intent == null) return new PulseDecision { Action = "none", Reason = "no_due_intent" };

            await _memory.RememberAsync(new MemoryItem
            {
                Kind = "intent_done",
                Key = intent.Key,
                Value = DateTime.UtcNow.ToString("o"),
                Summary = "delivered"
            }, ct).ConfigureAwait(false);

            return new PulseDecision
            {
                Action = "message",
                Text = "提醒：" + intent.Value,
                Reason = "user_intent"
            };
        }
    }
}
