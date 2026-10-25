using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    public sealed class BrainPipeClient
    {
        readonly string _pipe;
        readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public BrainPipeClient(string pipe) { _pipe = pipe; }

        public Task<string> AskAsync(string text, CancellationToken ct)
        {
            return RunAsync(new BrainPipeRequest { Op = "ask", Text = text }, null, ct);
        }

        public Task<string> RunToolAsync(string text, Func<BrainToolCall, CancellationToken, Task<bool>> approval, CancellationToken ct)
        {
            return RunAsync(new BrainPipeRequest { Op = "tool", Text = text }, approval, ct);
        }

        async Task<string> RunAsync(BrainPipeRequest req, Func<BrainToolCall, CancellationToken, Task<bool>> approval, CancellationToken ct)
        {
            using (var pipe = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync(3000, ct).ConfigureAwait(false);
                using (var reader = new System.IO.StreamReader(pipe, Encoding.UTF8))
                using (var writer = new System.IO.StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(req, _json)).ConfigureAwait(false);
                    while (true)
                    {
                        string line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (string.IsNullOrEmpty(line)) throw new System.IO.IOException("brain host closed pipe");
                        BrainPipeReply reply = JsonSerializer.Deserialize<BrainPipeReply>(line, _json);
                        if (reply == null) continue;
                        if (reply.Event == "approval")
                        {
                            bool ok = false;
                            if (approval != null)
                            {
                                var call = new BrainToolCall { Tool = reply.Tool, Args = reply.Args, Tier = PermissionTier.Full };
                                ok = await approval(call, ct).ConfigureAwait(false);
                            }
                            await writer.WriteLineAsync(JsonSerializer.Serialize(new BrainPipeReply { Event = "approval", Ok = ok }, _json)).ConfigureAwait(false);
                            continue;
                        }
                        if (reply.Event == "final")
                        {
                            if (!reply.Ok) throw new InvalidOperationException(reply.Error ?? "brain host failed");
                            return reply.Text ?? "";
                        }
                    }
                }
            }
        }
    }
}
