using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Contracts;

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
        if (repo == null) { Console.WriteLine("BRAIN CLIENT SMOKE: FAIL (repo not found)"); return 1; }
        string bodyDir = Path.Combine(repo, "ahz-scribe-body", "src");
        string bodyExe = Path.Combine(bodyDir, "bin", "Debug", "net10.0-windows", "AhzScribe.Body.exe");
        if (!File.Exists(bodyExe)) { Console.WriteLine("BRAIN CLIENT SMOKE: FAIL (body not built)"); return 1; }
        string testFile = Path.Combine(bodyDir, "_brain-client.txt");
        File.WriteAllText(testFile, "brain-body-ipc", new UTF8Encoding(false));
        Process body = null;
        try
        {
            var psi = new ProcessStartInfo(bodyExe) { WorkingDirectory = bodyDir, UseShellExecute = false };
            string dotnetRoot = Path.Combine(repo, "..", "local-agent", ".dotnet");
            if (Directory.Exists(dotnetRoot)) psi.Environment["DOTNET_ROOT"] = Path.GetFullPath(dotnetRoot);
            body = Process.Start(psi);
            using (var client = new BodyClient())
            {
                string error = "";
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        var result = client.ExecuteAsync(new ActionRequest
                        {
                            RequestId = "brain-smoke-1",
                            Tool = "read_file",
                            Tier = PermissionTier.ReadOnly,
                            Args = new System.Collections.Generic.Dictionary<string, object> { ["path"] = "_brain-client.txt" }
                        }, CancellationToken.None).GetAwaiter().GetResult();
                        bool ok = result != null && result.Ok && result.Output == "brain-body-ipc";
                        Console.WriteLine(ok ? "BRAIN CLIENT SMOKE: PASS" : "BRAIN CLIENT SMOKE: FAIL result");
                        return ok ? 0 : 1;
                    }
                    catch (Exception ex) { error = ex.Message; Thread.Sleep(250); }
                }
                Console.WriteLine("BRAIN CLIENT SMOKE: FAIL " + error);
                return 1;
            }
        }
        finally
        {
            try { if (body != null && !body.HasExited) body.Kill(true); } catch { }
            try { File.Delete(testFile); } catch { }
        }
    }
}
