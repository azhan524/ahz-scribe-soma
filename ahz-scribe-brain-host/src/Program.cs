using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Brain;
using AhzScribe.Contracts;

internal sealed class Echo : IBrainModel
{
    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        return Task.FromResult("[brain-host echo] " + user);
    }
}

internal static class Program
{
    public static int Main(string[] args)
    {
        string key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        string baseUrl = Environment.GetEnvironmentVariable("AHZ_MODEL_BASE") ?? "https://api.deepseek.com";
        string modelName = Environment.GetEnvironmentVariable("AHZ_MODEL_NAME") ?? "deepseek-v4-pro";
        string pipe = Environment.GetEnvironmentVariable("AHZ_BRAIN_PIPE") ?? "ahz-scribe-brain";
        string data = Path.Combine(AppContext.BaseDirectory, "_brain-data");
        Directory.CreateDirectory(data);
        var memory = new MemoryStore(Path.Combine(data, "memory.jsonl"));
        IBrainModel model = string.IsNullOrEmpty(key)
            ? (IBrainModel)new Echo()
            : new OpenAiToolModel(new ModelOptions { BaseUrl = baseUrl, Model = modelName, ApiKey = key });
        Console.WriteLine("brain host: pipe=" + pipe + " model=" + modelName);
        var server = new BrainHostServer(memory, model);
        server.RunAsync(pipe, CancellationToken.None).GetAwaiter().GetResult();
        return 0;
    }
}
