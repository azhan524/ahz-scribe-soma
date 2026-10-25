using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Contracts;

internal sealed class ReadThenFinishModel : IToolCallingModel
{
    public Task<BrainTurn> NextAsync(string system, IReadOnlyList<BrainTurnMessage> history, CancellationToken ct)
    {
        foreach (BrainTurnMessage m in history)
            if (m.Role == "tool")
                return Task.FromResult(new BrainTurn { Text = "文件内容：" + m.Content, Done = true });
        return Task.FromResult(new BrainTurn
        {
            Calls = new List<BrainToolCall>
            {
                new BrainToolCall
                {
                    Id = "real-1", Tool = "read_file", Tier = PermissionTier.ReadOnly,
                    Args = new Dictionary<string, object> { ["path"] = "_tool-loop.txt" }
                }
            }
        });
    }
}

internal sealed class NullMemory : IMemoryStore
{
    public Task<string> RememberAsync(MemoryItem item, CancellationToken ct) { return Task.FromResult(item.Id); }
    public Task<List<MemoryItem>> RetrieveAsync(string query, int topK, CancellationToken ct) { return Task.FromResult(new List<MemoryItem>()); }
    public Task<List<MemoryItem>> LoadAllAsync(CancellationToken ct) { return Task.FromResult(new List<MemoryItem>()); }
}

internal static class Program
{
    static string FindRepo()
    {
        string d = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(d); i++)
        {
            if (Directory.Exists(Path.Combine(d, "ahz-scribe-body", "src"))) return d;
            d = Directory.GetParent(d)?.FullName;
        }
        return null;
    }

    public static int Main()
    {
        string repo = FindRepo();
        if (repo == null) { Console.WriteLine("TOOL LOOP BODY: FAIL repo"); return 1; }
        string bodyDir = Path.Combine(repo, "ahz-scribe-body", "src");
        string bodyExe = Path.Combine(bodyDir, "bin", "Debug", "net10.0-windows", "AhzScribe.Body.exe");
        if (!File.Exists(bodyExe)) { Console.WriteLine("TOOL LOOP BODY: FAIL exe"); return 1; }
        string file = Path.Combine(bodyDir, "_tool-loop.txt");
        File.WriteAllText(file, "real-body-output", new UTF8Encoding(false));
        Process body = null;
        try
        {
            var psi = new ProcessStartInfo(bodyExe) { WorkingDirectory = bodyDir, UseShellExecute = false };
            string root = Path.Combine(repo, "..", "local-agent", ".dotnet");
            if (Directory.Exists(root)) psi.Environment["DOTNET_ROOT"] = Path.GetFullPath(root);
            body = Process.Start(psi);
            using (var client = new BodyClient())
            {
                string error = "";
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        var runner = new ToolAgentRunner(new NullMemory(), new ReadThenFinishModel(), client);
                        string answer = runner.RunAsync("读文件", CancellationToken.None).GetAwaiter().GetResult();
                        bool ok = answer.Contains("real-body-output");
                        Console.WriteLine(ok ? "TOOL LOOP BODY: PASS" : "TOOL LOOP BODY: FAIL " + answer);
                        return ok ? 0 : 1;
                    }
                    catch (Exception ex) { error = ex.Message; Thread.Sleep(250); }
                }
                Console.WriteLine("TOOL LOOP BODY: FAIL " + error);
                return 1;
            }
        }
        finally
        {
            try { if (body != null && !body.HasExited) body.Kill(true); } catch { }
            try { File.Delete(file); } catch { }
        }
    }
}
