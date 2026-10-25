using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// Brain host over a named pipe. Face sends ask/tool requests; approval is a two-way event.
    /// </summary>
    public sealed class BrainHostServer
    {
        readonly IMemoryStore _memory;
        readonly IBrainModel _model;
        readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public BrainHostServer(IMemoryStore memory, IBrainModel model)
        {
            _memory = memory; _model = model;
        }

        public async Task RunAsync(string pipeName, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 4,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                {
                    await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    await HandleAsync(pipe, ct).ConfigureAwait(false);
                }
            }
        }

        async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken ct)
        {
            using (var reader = new StreamReader(pipe, Encoding.UTF8))
            using (var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true })
            {
                string line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line)) return;
                BrainPipeRequest req = JsonSerializer.Deserialize<BrainPipeRequest>(line, _json);
                if (req == null || string.IsNullOrEmpty(req.Op)) return;

                string text = "";
                using (var host = new BrainHost(_memory, _model))
                {
                    host.Approval = async delegate(BrainToolCall call, CancellationToken inner)
                    {
                        var ev = new BrainPipeReply { Event = "approval", Tool = call.Tool, Args = call.Args };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(ev, _json)).ConfigureAwait(false);
                        await writer.FlushAsync().ConfigureAwait(false);
                        string answer = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(answer)) return false;
                        BrainPipeReply a = JsonSerializer.Deserialize<BrainPipeReply>(answer, _json);
                        return a != null && a.Ok;
                    };
                    try
                    {
                        text = req.Op == "tool"
                            ? await host.RunToolAsync(req.Text ?? "", ct).ConfigureAwait(false)
                            : await host.AskAsync(req.Text ?? "", ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new BrainPipeReply
                        { Event = "final", Ok = false, Error = ex.Message }, _json)).ConfigureAwait(false);
                        return;
                    }
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(new BrainPipeReply
                { Event = "final", Ok = true, Text = text }, _json)).ConfigureAwait(false);
            }
        }
    }
}
