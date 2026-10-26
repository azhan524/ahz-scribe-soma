using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    /// <summary>
    /// S4 trigger queue. Higher rank wins; equal ranks stay FIFO.
    /// user_message > intent_due > body_event > drive > timer.
    /// </summary>
    public sealed class PulseQueue
    {
        readonly List<PulseTrigger> _items = new List<PulseTrigger>();
        readonly object _gate = new object();

        public int Count { get { lock (_gate) return _items.Count; } }

        public void Enqueue(PulseTrigger t)
        {
            if (t == null) return;
            t.ReceivedAt = string.IsNullOrEmpty(t.ReceivedAt) ? DateTime.Now.ToString("o") : t.ReceivedAt;
            lock (_gate) _items.Add(t);
        }

        public PulseTrigger Dequeue()
        {
            lock (_gate)
            {
                if (_items.Count == 0) return null;
                int best = 0;
                for (int i = 1; i < _items.Count; i++)
                    if (Rank(_items[i].Type) < Rank(_items[best].Type)) best = i;
                PulseTrigger t = _items[best];
                _items.RemoveAt(best);
                return t;
            }
        }

        static int Rank(string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "user_message": return 0;
                case "intent_due": return 1;
                case "body_event": return 2;
                case "drive": return 3;
                default: return 4;
            }
        }
    }

    public sealed class PulseArbiter
    {
        readonly PulseScheduler _scheduler;
        readonly PulseQueue _queue;
        readonly Func<FaceState> _state;

        public PulseArbiter(PulseScheduler scheduler, PulseQueue queue, Func<FaceState> state)
        {
            _scheduler = scheduler; _queue = queue; _state = state;
        }

        public async Task<PulseDecision> EnqueueAsync(PulseTrigger trigger, CancellationToken ct)
        {
            if (trigger == null) return new PulseDecision { Action = "none", Reason = "null_trigger" };
            FaceState st = _state == null ? null : _state();
            if (st != null && !string.IsNullOrEmpty(st.ActiveTask) &&
                !string.Equals(trigger.Type, "user_message", StringComparison.OrdinalIgnoreCase))
                return new PulseDecision { Action = "none", Reason = "active_task_deferred" };

            _queue.Enqueue(trigger);
            PulseTrigger next = _queue.Dequeue();
            if (next == null) return new PulseDecision { Action = "none", Reason = "empty_queue" };
            return await _scheduler.TickAsync(ct).ConfigureAwait(false);
        }
    }
}
