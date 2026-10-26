using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Face;
using AhzScribe.Contracts;

internal sealed class EchoModel : IBrainModel
{
    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        return Task.FromResult("[face v0] " + user);
    }
}

internal sealed class ConsoleAudit : IAuditSink
{
    public Task<string> AppendAsync(string span, string tool, string decision, string result,
        string tier, Dictionary<string, object> args, string reason, CancellationToken ct)
    {
        Console.WriteLine("[audit] " + span + " " + decision + " " + result);
        return Task.FromResult("face-" + DateTime.UtcNow.Ticks);
    }
}

internal sealed class ConsoleSink : IPulseSink
{
    public Task EmitAsync(PulseEvent evt, CancellationToken ct)
    {
        Console.WriteLine("[pulse] " + evt.Summary);
        return Task.CompletedTask;
    }
}

internal sealed class NoDecider : IPulseDecider
{
    public Task<PulseDecision> DecideAsync(BodyStatus body, List<MemoryItem> memory, CancellationToken ct)
    {
        return Task.FromResult(new PulseDecision { Action = "none", Reason = "face_v0" });
    }
}

internal sealed class FaceBody : IBodyStatusSource
{
    public BodyStatus Snapshot() { return new BodyStatus { IdleSeconds = 0, ActiveTask = false }; }
}

internal sealed class CaptureModel : IBrainModel
{
    public string User = "";
    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        User = user ?? "";
        return Task.FromResult("ok");
    }
}
internal static class Program
{
    static string Env(string name, string fallback)
    {
        string v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    static IBrainModel CreateModel(out string name)
    {
        string key = Env("DEEPSEEK_API_KEY", Env("AHZ_MODEL_KEY", ""));
        string baseUrl = Env("AHZ_MODEL_BASE", "https://api.deepseek.com");
        string model = Env("AHZ_MODEL_NAME", "deepseek-flash(1)");
        name = model;
        if (string.IsNullOrEmpty(key)) return new EchoModel();
        return new OpenAiToolModel(new ModelOptions { BaseUrl = baseUrl, Model = model, ApiKey = key });
    }
    public static int Main(string[] args)
    {
        if (args != null && Array.IndexOf(args, "--memory-real") >= 0)
        {
            string key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) { Console.WriteLine("MEMORY REAL: SKIP (no key)"); return 0; }
            string dir = Path.Combine(AppContext.BaseDirectory, "_memory-real-" + Guid.NewGuid().ToString("N"));
            string sp = Path.Combine(dir, "sessions.jsonl");
            try
            {
                var store = new MemoryStore(Path.Combine(dir, "memory.jsonl"));
                store.RememberAsync(new MemoryItem { Kind = "episodic", Key = "episode:test", Value = "用户喜欢蓝鲸，并且正在做 Ahz Scribe", Summary = "episode test" }, CancellationToken.None).GetAwaiter().GetResult();
                using (var realModel = new OpenAiToolModel(new ModelOptions
                {
                    BaseUrl = Environment.GetEnvironmentVariable("AHZ_MODEL_BASE") ?? "https://api.deepseek.com",
                    Model = Environment.GetEnvironmentVariable("AHZ_MODEL_NAME") ?? "deepseek-v4-pro",
                    ApiKey = key
                }))
                {
                    var runner = new AgentRunner(store, realModel);
                    string answer = runner.RunAsync("根据记忆，我喜欢什么？", CancellationToken.None).GetAwaiter().GetResult();
                    bool ok = answer != null && answer.Contains("蓝鲸");
                    Console.WriteLine(ok ? "MEMORY REAL: PASS" : "MEMORY REAL: FAIL " + answer);
                    return ok ? 0 : 1;
                }
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
        if (args != null && Array.IndexOf(args, "--proactive-test") >= 0)
        {
            DateTime testNow = DateTime.Today.AddHours(12);
            var st = new FaceState { ProactiveCount = 2, LastProactiveDate = testNow.ToString("yyyy-MM-dd") };
            bool cap = !ProactiveGate.Allow(st, testNow);
            var paused = new FaceState { ProactivePausedUntil = testNow.AddHours(1).ToString("o") };
            bool pause = !ProactiveGate.Allow(paused, testNow);
            var normal = new FaceState { ProactiveCount = 0, LastProactiveDate = testNow.ToString("yyyy-MM-dd") };
            bool ok = cap && pause && ProactiveGate.Allow(normal, testNow);
            var recovered = new FaceState { ProactiveCount = 0, UnansweredProactive = 0, LastProactiveDate = testNow.ToString("yyyy-MM-dd"), ProactivePausedUntil = testNow.AddMinutes(-1).ToString("o") };
            bool recovery = ProactiveGate.Allow(recovered, testNow);
            ok = ok && recovery;
            Console.WriteLine("cap=" + cap + " paused=" + pause + " recovery=" + recovery);
            Console.WriteLine(ok ? "PROACTIVE GATE: PASS" : "PROACTIVE GATE: FAIL");
            return ok ? 0 : 1;
        }
        if (args != null && Array.IndexOf(args, "--body-test") >= 0)
        {
            var sensor = new BodySensor(Environment.CurrentDirectory);
            BodyStatus s = sensor.Snapshot();
            bool ok = s != null && s.MemoryMb > 0 && s.DiskFreeGb >= 0 && s.AgentProcessAlive;
            Console.WriteLine("idle=" + s.IdleSeconds + " mem=" + s.MemoryMb + " disk=" + s.DiskFreeGb + " net=" + s.NetworkOnline);
            Console.WriteLine(ok ? "S3 BODY: PASS" : "S3 BODY: FAIL");
            return ok ? 0 : 1;
        }
        if (args != null && Array.IndexOf(args, "--policy-test") >= 0)
        {
            var mem = new List<MemoryItem> { new MemoryItem { Kind = "semantic", Key = "pref", Value = "用户喜欢蓝鲸" } };
            bool yes = ProactivePolicy.ReferencesMemory("提醒：蓝鲸这件事", mem);
            bool no = ProactivePolicy.ReferencesMemory("你好呀", mem);
            Console.WriteLine("policy yes=" + yes + " no=" + no);
            Console.WriteLine(yes && !no ? "PROACTIVE POLICY: PASS" : "PROACTIVE POLICY: FAIL");
            return yes && !no ? 0 : 1;
        }
        if (args != null && Array.IndexOf(args, "--drive-test") >= 0)
        {
            var mem = new List<MemoryItem>
            {
                new MemoryItem { Kind = "semantic", Key = "a", Value = "x", Status = "disputed" },
                new MemoryItem { Kind = "semantic", Key = "b", Value = "y", Status = "active" }
            };
            DriveGoal g = DriveEngine.Pick(mem);
            bool ok = g != null && g.Type == "contradiction" && g.Score > 0;
            Console.WriteLine("drive=" + (g == null ? "null" : g.Type + " " + g.Score.ToString("F2")));
            Console.WriteLine(ok ? "DRIVE ENGINE: PASS" : "DRIVE ENGINE: FAIL");
            return ok ? 0 : 1;
        }
        if (args != null && Array.IndexOf(args, "--pulse-test") >= 0)
        {
            var q = new PulseQueue();
            q.Enqueue(new PulseTrigger { Type = "timer" });
            q.Enqueue(new PulseTrigger { Type = "drive" });
            q.Enqueue(new PulseTrigger { Type = "intent_due" });
            q.Enqueue(new PulseTrigger { Type = "body_event" });
            q.Enqueue(new PulseTrigger { Type = "user_message" });
            string order = "";
            PulseTrigger t;
            while ((t = q.Dequeue()) != null) order += t.Type + ",";
            bool orderOk = order == "user_message,intent_due,body_event,drive,timer,";
            var body = new FaceBody();
            var mem = new MemoryStore(Path.Combine(Path.GetTempPath(), "ahz-pulse-" + Guid.NewGuid().ToString("N"), "memory.jsonl"));
            var sched = new PulseScheduler(body, mem, new ConsoleAudit(), new ConsoleSink(), new NoDecider());
            var arb = new PulseArbiter(sched, new PulseQueue(), delegate { return new FaceState { ActiveTask = "running" }; });
            var d = arb.EnqueueAsync(new PulseTrigger { Type = "timer" }, CancellationToken.None).GetAwaiter().GetResult();
            bool deferOk = d != null && d.Reason == "active_task_deferred";
            Console.WriteLine("queue_order=" + order + " deferred=" + deferOk);
            Console.WriteLine(orderOk && deferOk ? "PULSE QUEUE: PASS" : "PULSE QUEUE: FAIL");
            sched.Dispose(); mem.Dispose();
            return orderOk && deferOk ? 0 : 1;
        }
        if (args != null && Array.IndexOf(args, "--state-test") >= 0)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "_state-test-" + Guid.NewGuid().ToString("N"));
            string sp = Path.Combine(dir, "sessions.jsonl");
            try
            {
                FaceSession.Append(sp, new FaceMessage { Role = "你", Text = "状态测试", Kind = "chat" });
                FaceSession.SetFocus(sp, "状态测试");
                FaceSession.BeginTask(sp, "running");
                OpenIntent it = FaceSession.AddIntent(sp, "提醒测试", "");
                FaceSession.SetPulseTrigger(sp, "timer", "state-test");
                FaceState a = FaceSession.LoadState(sp);
                bool persisted = a != null && a.CurrentFocus == "状态测试" && a.ActiveTask == "running" &&
                    a.OpenIntents != null && a.OpenIntents.Count > 0 && a.LastPulseTrigger != null &&
                    File.Exists(Path.Combine(dir, "current_state.json"));
                FaceSession.EndTask(sp);
                FaceState b = FaceSession.LoadState(sp);
                bool cleared = b != null && string.IsNullOrEmpty(b.ActiveTask);
                Console.WriteLine("state_file=" + File.Exists(Path.Combine(dir, "current_state.json")) +
                    " focus=" + (a == null ? "null" : a.CurrentFocus) +
                    " active=" + (a == null ? "null" : a.ActiveTask) +
                    " intents=" + (a == null || a.OpenIntents == null ? -1 : a.OpenIntents.Count));
                Console.WriteLine(persisted && cleared ? "S0 STATE: PASS" : "S0 STATE: FAIL");
                return persisted && cleared ? 0 : 1;
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
        if (args != null && Array.IndexOf(args, "--session-test") >= 0)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "_session-test-" + Guid.NewGuid().ToString("N"));
            string sp = Path.Combine(dir, "sessions.jsonl");
            try
            {
                for (int i = 0; i < 41; i++)
                    FaceSession.Append(sp, new FaceMessage { Role = "你", Text = "m" + i, Kind = "chat" });
                List<FaceMessage> msgs = FaceSession.Load(sp, 100);
                FaceState st = FaceSession.LoadState(sp);
                int eps = 0; string last = "";
                foreach (FaceMessage m in msgs) if (m.Episode != last) { eps++; last = m.Episode; }
                var mstore = new MemoryStore(Path.Combine(dir, "memory.jsonl"));
                mstore.RememberAsync(new MemoryItem { Kind = "episodic", Key = "episode:" + msgs[0].Episode, Value = "用户问过 m0", Summary = "episode test" }, CancellationToken.None).GetAwaiter().GetResult();
                var capture = new CaptureModel();
                var runner = new AgentRunner(mstore, capture);
                runner.RunAsync("m0", CancellationToken.None).GetAwaiter().GetResult();
                bool memOk = capture.User.Contains("用户问过 m0");
                bool ok = msgs.Count == 41 && eps == 2 && st != null && st.MessageCount == 1 && memOk;
                Console.WriteLine("msgs=" + msgs.Count + " eps=" + eps + " state=" + (st == null ? "null" : st.MessageCount.ToString()) + " memory=" + memOk);
                Console.WriteLine(ok ? "FACE SESSION EPISODE: PASS" : "FACE SESSION EPISODE: FAIL");
                return ok ? 0 : 1;
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
        if (args != null && Array.IndexOf(args, "--ui") >= 0)
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new AhzScribe.Face.FaceForm());
            return 0;
        }

        string root = Path.Combine(AppContext.BaseDirectory, "_face-data");
        Directory.CreateDirectory(root);
        var memory = new MemoryStore(Path.Combine(root, "memory.jsonl"));
        IBrainModel model = CreateModel(out string modelName);
        var host = new BrainHost(memory, model);
        host.Trace = delegate(string line) { Console.WriteLine("[trace] " + line); };
        var pulse = new PulseScheduler(new FaceBody(), memory, new ConsoleAudit(), new ConsoleSink(), new NoDecider());

        string brainPipe = Env("AHZ_BRAIN_PIPE", "");
        BrainPipeClient pipeClient = string.IsNullOrEmpty(brainPipe) ? null : new BrainPipeClient(brainPipe);
        Console.WriteLine("Ahz Scribe face v0 · model=" + modelName + (pipeClient == null ? "" : " · brain=" + brainPipe));
        Console.WriteLine("命令：/remember <kind> <key> <value> | /memory <query> | /ask <text> | /tool <text> | /pulse | /quit");
        while (true)
        {
            Console.Write("> ");
            string line = Console.ReadLine();
            if (line == null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            try
            {
                if (line == "/quit" || line == "/exit") break;
                if (line.StartsWith("/remember ", StringComparison.Ordinal))
                {
                    string[] p = line.Split(new[] { ' ' }, 4);
                    if (p.Length < 4) { Console.WriteLine("用法：/remember <kind> <key> <value>"); continue; }
                    string id = memory.RememberAsync(new MemoryItem { Kind = p[1], Key = p[2], Value = p[3], Summary = p[2] + "=" + p[3] }, CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine("已记住 " + id);
                    continue;
                }
                if (line.StartsWith("/memory ", StringComparison.Ordinal))
                {
                    var hits = memory.RetrieveAsync(line.Substring(8), 8, CancellationToken.None).GetAwaiter().GetResult();
                    foreach (MemoryItem m in hits) Console.WriteLine("- " + m.Kind + " / " + m.Key + " = " + m.Value);
                    continue;
                }
                if (line.StartsWith("/ask ", StringComparison.Ordinal))
                {
                    Console.WriteLine((pipeClient == null ? host.AskAsync(line.Substring(5), CancellationToken.None) : pipeClient.AskAsync(line.Substring(5), CancellationToken.None)).GetAwaiter().GetResult());
                    continue;
                }
                if (line.StartsWith("/tool ", StringComparison.Ordinal))
                {
                    if (pipeClient == null && !(model is IToolCallingModel)) { Console.WriteLine("当前模型不支持工具调用。"); continue; }
                    host.Approval = delegate(BrainToolCall call, CancellationToken ct)
                    {
                        Console.Write("批准 " + call.Tool + " ? [y/N] ");
                        string a = Console.ReadLine();
                        return Task.FromResult(a != null && a.Trim().Equals("y", StringComparison.OrdinalIgnoreCase));
                    };
                    Console.WriteLine((pipeClient == null ? host.RunToolAsync(line.Substring(6), CancellationToken.None) : pipeClient.RunToolAsync(line.Substring(6), host.Approval, CancellationToken.None)).GetAwaiter().GetResult());
                    continue;
                }
                if (line == "/pulse")
                {
                    pulse.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
                    continue;
                }
                Console.WriteLine((pipeClient == null ? host.AskAsync(line, CancellationToken.None) : pipeClient.AskAsync(line, CancellationToken.None)).GetAwaiter().GetResult());
            }
            catch (Exception ex) { Console.WriteLine("错误：" + ex.Message); }
        }
        pulse.Dispose();
        return 0;
    }
}
