using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
