using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// Brain-side tool loop v0. Model proposes tool calls; body executes; results go back to model.
    /// Brain never touches files, shell, or desktop directly.
    /// </summary>
    public sealed class ToolAgentRunner
    {
        readonly IMemoryStore _memory;
        readonly IToolCallingModel _model;
        readonly IActionExecutor _body;
        readonly int _maxSteps;
        public Func<BrainToolCall, CancellationToken, Task<bool>> Approval { get; set; }
        public Action<string> Trace { get; set; }
        public SelfIterationPolicy Policy { get; set; }
        public Func<BrainToolCall, CancellationToken, Task<bool>> Propose { get; set; }

        public ToolAgentRunner(IMemoryStore memory, IToolCallingModel model, IActionExecutor body, int maxSteps = 8)
        {
            _memory = memory; _model = model; _body = body; _maxSteps = maxSteps < 1 ? 1 : maxSteps;
        }

        public async Task<string> RunAsync(string userText, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userText)) return "";
            List<MemoryItem> hits = await _memory.RetrieveAsync(userText, 6, ct).ConfigureAwait(false);
            string memory = "";
            if (hits != null && hits.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("相关记忆：");
                foreach (MemoryItem m in hits)
                    sb.AppendLine("- " + (m.Kind ?? "") + " / " + (m.Key ?? "") + " = " + (m.Value ?? ""));
                memory = sb.ToString();
            }

            string system = "你是 Ahz Scribe。需要事实时先调工具，不要猜。工具结果以 tool 消息返回。";
            var history = new List<BrainTurnMessage>
            {
                new BrainTurnMessage { Role = "user", Content = memory + "\n用户：" + userText }
            };
            string lastText = "";
            string lastAuditId = "";
            for (int step = 0; step < _maxSteps; step++)
            {
                ct.ThrowIfCancellationRequested();
                BrainTurn turn = await _model.NextAsync(system, history, ct).ConfigureAwait(false);
                if (turn == null) return lastText;
                if (!string.IsNullOrEmpty(turn.Text)) lastText = turn.Text;
                if (turn.Done || turn.Calls == null || turn.Calls.Count == 0) return lastText;

                history.Add(new BrainTurnMessage { Role = "assistant", Content = turn.Text ?? "" });
                foreach (BrainToolCall call in turn.Calls)
                {
                    if (call == null || string.IsNullOrEmpty(call.Tool))
                    {
                        history.Add(new BrainTurnMessage { Role = "tool", ToolName = "", Content = "invalid tool call" });
                        continue;
                    }
                    if (Policy != null && call.Tool == "propose_upgrade")
                    {
                        if (Trace != null) Trace("→ propose_upgrade");
                        bool approved = false;
                        if (Propose != null) { try { approved = await Propose(call, ct).ConfigureAwait(false); } catch { approved = false; } }
                        if (Trace != null) Trace(approved ? "← APPROVED" : "← DENIED");
                        history.Add(new BrainTurnMessage { Role = "tool", ToolName = call.Tool, ToolCallId = call.Id, Content = approved ? "APPROVED" : "DENIED: user did not approve the upgrade proposal" });
                        continue;
                    }
                    if (Policy != null && IsWriteTool(call.Tool))
                    {
                        string p = call.Args != null && call.Args.ContainsKey("path") ? Convert.ToString(call.Args["path"]) : "";
                        string why;
                        if (!Policy.Check(p, out why))
                        {
                            history.Add(new BrainTurnMessage { Role = "tool", ToolName = call.Tool, ToolCallId = call.Id, Content = "DENIED: " + why });
                            continue;
                        }
                    }
                    var req = new ActionRequest
                    {
                        RequestId = string.IsNullOrEmpty(call.Id) ? "brain-" + step + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) : call.Id,
                        Tool = call.Tool,
                        Tier = call.Tier,
                        Approved = call.Approved,
                        Args = call.Args ?? new Dictionary<string, object>(),
                        LinkedAuditId = call.Tool == "rollback_self" ? lastAuditId : null
                    };
                    string result;
                    if (!call.Approved && call.Tier == PermissionTier.Full && Approval != null)
                    {
                        bool approved = false;
                        try { approved = await Approval(call, ct).ConfigureAwait(false); } catch { approved = false; }
                        if (!approved)
                        {
                            history.Add(new BrainTurnMessage { Role = "tool", ToolName = call.Tool, ToolCallId = call.Id, Content = "DENIED: user did not approve" });
                            continue;
                        }
                        call.Approved = true;
                        req.Approved = true;
                    }
                    if (Trace != null) Trace("→ " + call.Tool);
                    try
                    {
                        ActionResult r = await _body.ExecuteAsync(req, ct).ConfigureAwait(false);
                        result = r == null ? "null result" : (r.Ok ? (r.Output ?? "") : ("ERROR: " + (r.Error ?? "failed")));
                        if (r != null && !string.IsNullOrEmpty(r.AuditId)) lastAuditId = r.AuditId;
                    }
                    catch (Exception ex) { result = "ERROR: " + ex.Message; }
                    if (Trace != null) Trace("← " + (result.Length > 400 ? result.Substring(0, 400) + "..." : result));
                    history.Add(new BrainTurnMessage { Role = "tool", ToolName = call.Tool, ToolCallId = call.Id, Content = result });
                }
            }
            return lastText.Length > 0 ? lastText : "工具循环超过 " + _maxSteps + " 步，已停止。";
        }
        static bool IsWriteTool(string name)
        {
            return name == "write_file" || name == "edit_file" || name == "make_dir" || name == "delete_file" || name == "copy_file" || name == "move_file";
        }
    }
}
