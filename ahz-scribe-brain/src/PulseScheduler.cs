using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// Brain-side pulse loop. It has no tool/file/desktop access; it can only emit a message.
    /// </summary>
    public sealed class PulseScheduler : IDisposable
    {
        readonly IBodyStatusSource _body;
        readonly IMemoryStore _memory;
        readonly IAuditSink _audit;
        readonly IPulseSink _sink;
        readonly IPulseDecider _decider;
        readonly SemaphoreSlim _busy = new SemaphoreSlim(1, 1);
        Timer _timer;

        public PulseScheduler(IBodyStatusSource body, IMemoryStore memory, IAuditSink audit,
            IPulseSink sink, IPulseDecider decider)
        {
            _body = body; _memory = memory; _audit = audit; _sink = sink; _decider = decider;
        }

        public void Start(int periodMs)
        {
            if (_timer != null) return;
            if (periodMs < 1000) periodMs = 1000;
            _timer = new Timer(delegate { TickAsync(CancellationToken.None).GetAwaiter().GetResult(); }, null, periodMs, periodMs);
        }

        public void Stop()
        {
            Timer t = _timer;
            _timer = null;
            if (t != null) t.Dispose();
        }

        public async Task<PulseDecision> TickAsync(CancellationToken ct)
        {
            if (!await _busy.WaitAsync(0, ct).ConfigureAwait(false)) return null;
            try
            {
                BodyStatus body = _body == null ? new BodyStatus() : _body.Snapshot();
                if (body == null) body = new BodyStatus();
                if (body.ActiveTask)
                {
                    await _audit.AppendAsync("pulse", "active_task", "skipped", "", "read-only",
                        new Dictionary<string, object>(), "active task", ct).ConfigureAwait(false);
                    return new PulseDecision { Action = "none", Reason = "active_task" };
                }

                List<MemoryItem> memory = await _memory.LoadAllAsync(ct).ConfigureAwait(false);
                PulseDecision d = await _decider.DecideAsync(body, memory, ct).ConfigureAwait(false);
                if (d == null) d = new PulseDecision { Action = "none", Reason = "null" };
                string action = string.IsNullOrEmpty(d.Action) ? "none" : d.Action.ToLowerInvariant();
                d.Action = action;

                await _audit.AppendAsync("pulse_model", "decide", "allowed", action, "read-only",
                    new Dictionary<string, object>(), d.Reason ?? "", ct).ConfigureAwait(false);
                if (action == "message")
                {
                    if (string.IsNullOrWhiteSpace(d.Text))
                    {
                        await _audit.AppendAsync("proactive_message", "message", "blocked", "empty_text",
                            "read-only", new Dictionary<string, object>(), d.Reason ?? "", ct).ConfigureAwait(false);
                        return new PulseDecision { Action = "none", Reason = "empty_text" };
                    }
                    if (_sink != null)
                    {
                        await _sink.EmitAsync(new PulseEvent
                        {
                            Kind = "message",
                            Summary = d.Text,
                            At = DateTimeOffset.UtcNow
                        }, ct).ConfigureAwait(false);
                        await _audit.AppendAsync("proactive_message", "message", "sent", d.Text, "read-only",
                            new Dictionary<string, object>(), d.Reason ?? "", ct).ConfigureAwait(false);
                    }
                }
                return d;
            }
            catch (Exception ex)
            {
                try
                {
                    await _audit.AppendAsync("pulse", "error", "error", ex.Message, "read-only",
                        new Dictionary<string, object>(), "pulse exception", ct).ConfigureAwait(false);
                }
                catch { }
                return new PulseDecision { Action = "none", Reason = "error:" + ex.Message };
            }
            finally { _busy.Release(); }
        }

        public void Dispose() { Stop(); _busy.Dispose(); }
    }
}
