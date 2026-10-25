using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Contracts;

internal static class Program
{
    sealed class BodySource : IBodyStatusSource
    {
        public BodyStatus Snapshot() { return new BodyStatus { IdleSeconds = 900, ActiveTask = false }; }
    }

    sealed class Audit : IAuditSink
    {
        public readonly List<string> Items = new List<string>();
        public Task<string> AppendAsync(string span, string tool, string decision, string result,
            string tier, Dictionary<string, object> args, string reason, CancellationToken ct)
        {
            Items.Add(span + ":" + decision + ":" + result);
            return Task.FromResult("a" + Items.Count);
        }
    }

    sealed class Sink : IPulseSink
    {
        public readonly List<string> Messages = new List<string>();
        public Task EmitAsync(PulseEvent evt, CancellationToken ct)
        {
            Messages.Add(evt.Summary);
            return Task.CompletedTask;
        }
    }

    sealed class Decider : IPulseDecider
    {
        public Task<PulseDecision> DecideAsync(BodyStatus body, List<MemoryItem> memory, CancellationToken ct)
        {
            return Task.FromResult(new PulseDecision { Action = "message", Text = "蓝鲸 的项目可以继续", Reason = "test" });
        }
    }

    sealed class Model : IBrainModel
    {
        public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
        {
            return Task.FromResult(user.Contains("蓝鲸") ? "记忆命中" : "没有记忆");
        }
    }
    sealed class FakeToolModel : IToolCallingModel
    {
        public Task<BrainTurn> NextAsync(string system, IReadOnlyList<BrainTurnMessage> history, CancellationToken ct)
        {
            foreach (BrainTurnMessage m in history)
                if (m.Role == "tool")
                    return Task.FromResult(new BrainTurn { Text = "读到了：" + m.Content, Done = true });
            return Task.FromResult(new BrainTurn
            {
                Calls = new List<BrainToolCall>
                {
                    new BrainToolCall { Id = "c1", Tool = "read_file", Tier = PermissionTier.ReadOnly,
                        Args = new Dictionary<string, object> { ["path"] = "a.txt" } }
                }
            });
        }
    }

    sealed class FakeBody : IActionExecutor
    {
        public int Calls;
        public readonly List<ActionRequest> Requests = new List<ActionRequest>();
        public Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken ct)
        {
            Calls++; Requests.Add(request);
            return Task.FromResult(new ActionResult { Ok = true, Output = "hello-file", AuditId = "audit-" + Calls });
        }
    }
    sealed class RollbackModel : IToolCallingModel
    {
        public Task<BrainTurn> NextAsync(string system, IReadOnlyList<BrainTurnMessage> history, CancellationToken ct)
        {
            int n = 0; foreach (BrainTurnMessage m in history) if (m.Role == "tool") n++;
            if (n == 0) return Task.FromResult(new BrainTurn { Calls = new List<BrainToolCall> { new BrainToolCall { Id = "e1", Tool = "edit_file", Tier = PermissionTier.WorkspaceWrite, Args = new Dictionary<string, object> { ["path"] = "a.txt", ["old_string"] = "a", ["new_string"] = "b" } } } });
            if (n == 1) return Task.FromResult(new BrainTurn { Calls = new List<BrainToolCall> { new BrainToolCall { Id = "r1", Tool = "rollback_self", Tier = PermissionTier.ReadOnly, Args = new Dictionary<string, object>() } } });
            return Task.FromResult(new BrainTurn { Text = "done", Done = true });
        }
    }

    public static int Main()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "_brain-core-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var mem = new MemoryStore(Path.Combine(dir, "memory.jsonl"));
            mem.RememberAsync(new MemoryItem { Kind = "fact", Key = "蓝鲸", Value = "Ahz Scribe 最终版", Summary = "蓝鲸指最终版" }, CancellationToken.None).GetAwaiter().GetResult();
            var hits = mem.RetrieveAsync("蓝鲸 是什么", 5, CancellationToken.None).GetAwaiter().GetResult();
            bool memoryOk = hits.Count == 1 && hits[0].Key == "蓝鲸";
            mem.RememberAsync(new MemoryItem { Kind = "episodic", Key = "episode:ep-1", Value = "用户喜欢蓝鲸", Summary = "episode ep-1" }, CancellationToken.None).GetAwaiter().GetResult();
            var episodeHits = mem.RetrieveAsync("蓝鲸 episode", 5, CancellationToken.None).GetAwaiter().GetResult();
            bool episodeOk = episodeHits.Exists(x => x.Key == "episode:ep-1");

            var audit = new Audit();
            var sink = new Sink();
            var pulse = new PulseScheduler(new BodySource(), mem, audit, sink, new Decider());
            var decision = pulse.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
            bool pulseOk = decision != null && decision.Action == "message" && sink.Messages.Count == 1;
            var runner = new AgentRunner(mem, new Model());
            string answer = runner.RunAsync("蓝鲸 是什么", CancellationToken.None).GetAwaiter().GetResult();
            bool agentOk = answer == "记忆命中";
            var linkBody = new FakeBody();
            var linkRunner = new ToolAgentRunner(mem, new RollbackModel(), linkBody);
            linkRunner.RunAsync("x", CancellationToken.None).GetAwaiter().GetResult();
            bool linkOk = linkBody.Requests.Count == 2 && linkBody.Requests[1].LinkedAuditId == "audit-1";
            var toolBody = new FakeBody();
            var toolRunner = new ToolAgentRunner(mem, new FakeToolModel(), toolBody);
            string toolAnswer = toolRunner.RunAsync("读 a.txt", CancellationToken.None).GetAwaiter().GetResult();
            bool toolOk = toolAnswer.Contains("hello-file") && toolBody.Calls == 1;
            string modelJson = "{\"choices\":[{\"message\":{\"content\":\"\",\"tool_calls\":[{\"id\":\"call_1\",\"function\":{\"name\":\"read_file\",\"arguments\":\"{\\\"path\\\":\\\"a.txt\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}";
            BrainTurn parsed = OpenAiToolModel.ParseResponse(modelJson);
            bool modelOk = parsed.Calls.Count == 1 && parsed.Calls[0].Tool == "read_file" && parsed.Calls[0].Tier == PermissionTier.ReadOnly && !parsed.Calls[0].Approved;
            var policy = new SelfIterationPolicy();
            policy.Begin(dir);
            string why;
            bool policyOk = policy.Check("src/AhzScribe.cs", out why)
                && !policy.Check("AGENTS.md", out why)
                && !policy.Check("devtools/bench/x.cs", out why)
                && !policy.Check("../outside.txt", out why);
            bool ok = memoryOk && episodeOk && pulseOk && agentOk && toolOk && modelOk && policyOk && linkOk && audit.Items.Count >= 2;
            Console.WriteLine(ok ? "BRAIN MEMORY/PULSE: PASS" : "BRAIN MEMORY/PULSE: FAIL");
            if (!ok) Console.WriteLine("memory=" + memoryOk + " pulse=" + pulseOk + " agent=" + agentOk + " tool=" + toolOk + " episode=" + episodeOk + " model=" + modelOk + " policy=" + policyOk + " link=" + linkOk + " audit=" + audit.Items.Count);
            return ok ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
