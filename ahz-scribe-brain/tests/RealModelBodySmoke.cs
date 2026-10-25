using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Contracts;

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
        string key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) { Console.WriteLine("REAL MODEL BODY: SKIP (no key)"); return 0; }
        string repo = FindRepo();
        if (repo == null) { Console.WriteLine("REAL MODEL BODY: FAIL repo"); return 1; }
        string bodyDir = Path.Combine(repo, "ahz-scribe-body", "src");
        string bodyExe = Path.Combine(bodyDir, "bin", "Debug", "net10.0-windows", "AhzScribe.Body.exe");
        if (!File.Exists(bodyExe)) { Console.WriteLine("REAL MODEL BODY: FAIL exe"); return 1; }
        string file = Path.Combine(bodyDir, "_real-model-tool.txt");
        File.WriteAllText(file, "REAL-MODEL-TOOL-OK", new UTF8Encoding(false));
        Process body = null;
        try
        {
            var psi = new ProcessStartInfo(bodyExe) { WorkingDirectory = bodyDir, UseShellExecute = false };
            string root = Path.Combine(repo, "..", "local-agent", ".dotnet");
            if (Directory.Exists(root)) psi.Environment["DOTNET_ROOT"] = Path.GetFullPath(root);
            body = Process.Start(psi);
            using (var client = new BodyClient())
            using (var model = new OpenAiToolModel(new ModelOptions
            {
                BaseUrl = Environment.GetEnvironmentVariable("AHZ_MODEL_BASE") ?? "https://api.deepseek.com",
                Model = Environment.GetEnvironmentVariable("AHZ_MODEL_NAME") ?? "deepseek-v4-pro",
                ApiKey = key,
                Temperature = 0.2,
                MaxTokens = 512
            }))
            {
                var runner = new ToolAgentRunner(new NullMemory(), model, client, 6);
                string answer = runner.RunAsync("请读取 _real-model-tool.txt，把文件内容原样告诉我。", CancellationToken.None).GetAwaiter().GetResult();
                bool ok = answer.Contains("REAL-MODEL-TOOL-OK");
                Console.WriteLine(ok ? "REAL MODEL BODY: PASS" : "REAL MODEL BODY: FAIL " + answer);
                return ok ? 0 : 1;
            }
        }
        finally
        {
            try { if (body != null && !body.HasExited) body.Kill(true); } catch { }
            try { File.Delete(file); } catch { }
        }
    }
}
