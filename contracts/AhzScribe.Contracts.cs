using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AhzScribe.Contracts
{
    public enum PermissionTier
    {
        ReadOnly,
        WorkspaceWrite,
        Full
    }

    public sealed class ActionRequest
    {
        public string RequestId { get; set; }
        public string Tool { get; set; }
        public Dictionary<string, object> Args { get; set; }
        public string Reason { get; set; }
        public PermissionTier Tier { get; set; }
        public string SessionId { get; set; }
        public string Cwd { get; set; }
        // Set by face after the user approves a dangerous action; body never approves itself.
        public bool Approved { get; set; }
        public string LinkedAuditId { get; set; }

        public ActionRequest()
        {
            Args = new Dictionary<string, object>(StringComparer.Ordinal);
        }
    }

    public sealed class ActionResult
    {
        public string RequestId { get; set; }
        public bool Ok { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public string AuditId { get; set; }
        public long DurationMs { get; set; }
    }

    public interface IActionExecutor
    {
        Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken ct);
    }

    public interface IAuditSink
    {
        // Returns the audit id. Implementations must append, never rewrite.
        Task<string> AppendAsync(string span, string tool, string decision, string result,
            string tier, Dictionary<string, object> args, string reason, CancellationToken ct);
    }

    public sealed class BodyEnvelope
    {
        public string Action { get; set; }
        public ActionRequest Request { get; set; }
    }

    public sealed class BodyReply
    {
        public bool Ok { get; set; }
        public ActionResult Result { get; set; }
        public string Error { get; set; }
    }

    public sealed class BodyStatus
    {
        public bool Locked { get; set; }
        public int IdleSeconds { get; set; }
        public string ForegroundApp { get; set; }
        public string ForegroundTitle { get; set; }
        public bool WorkspaceDirty { get; set; }
        public bool NetworkOnline { get; set; }
        public bool ActiveTask { get; set; }
        public double CpuPercent { get; set; }
        public long MemoryMb { get; set; }
        public double DiskFreeGb { get; set; }
        public double BatteryPercent { get; set; }
        public bool AgentProcessAlive { get; set; }
    }

    public sealed class MemoryItem
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Summary { get; set; }
        public string Status { get; set; }
        public string SourceAuditId { get; set; }
        public string DisputedWith { get; set; }
        public string Ts { get; set; }
    }

    public interface IMemoryStore
    {
        Task<string> RememberAsync(MemoryItem item, CancellationToken ct);
        Task<List<MemoryItem>> RetrieveAsync(string query, int topK, CancellationToken ct);
        Task<List<MemoryItem>> LoadAllAsync(CancellationToken ct);
    }

    public sealed class PulseDecision
    {
        public string Action { get; set; } // none / message / wait
        public string Text { get; set; }
        public string Reason { get; set; }
    }

    public interface IBrainModel
    {
        Task<string> CompleteAsync(string system, string user, CancellationToken ct);
    }

    public sealed class BrainToolCall
    {
        public string Id { get; set; }
        public string Tool { get; set; }
        public PermissionTier Tier { get; set; } = PermissionTier.ReadOnly;
        public bool Approved { get; set; }
        public string LinkedAuditId { get; set; }
        public Dictionary<string, object> Args { get; set; }
    }

    public sealed class BrainTurn
    {
        public string Text { get; set; }
        public bool Done { get; set; }
        public List<BrainToolCall> Calls { get; set; }
    }

    public sealed class BrainTurnMessage
    {
        public string Role { get; set; } // user / assistant / tool
        public string Content { get; set; }
        public string ToolName { get; set; }
        public string ToolCallId { get; set; }
    }

    public sealed class BrainPipeRequest
    {
        public string Op { get; set; } // ask / tool
        public string Text { get; set; }
    }

    public sealed class BrainPipeReply
    {
        public string Event { get; set; } // approval / final
        public bool Ok { get; set; }
        public string Text { get; set; }
        public string Error { get; set; }
        public string Tool { get; set; }
        public Dictionary<string, object> Args { get; set; }
    }
    public interface IToolCallingModel
    {
        Task<BrainTurn> NextAsync(string system, IReadOnlyList<BrainTurnMessage> history, CancellationToken ct);
    }
    public interface IPulseDecider
    {
        Task<PulseDecision> DecideAsync(BodyStatus body, List<MemoryItem> memory, CancellationToken ct);
    }
    public interface IBodyStatusSource
    {
        BodyStatus Snapshot();
    }

    public sealed class PulseEvent
    {
        public string IntentId { get; set; }
        public string Kind { get; set; }
        public string Summary { get; set; }
        public DateTimeOffset At { get; set; }
    }

    public interface IPulseSink
    {
        Task EmitAsync(PulseEvent evt, CancellationToken ct);
    }
}
