using System;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// Brain host v0. It owns the body client and the tool loop; face only talks to this class.
    /// </summary>
    public sealed class BrainHost : IDisposable
    {
        readonly IMemoryStore _memory;
        readonly IBrainModel _model;
        readonly IActionExecutor _body;
        readonly bool _ownsBody;
        readonly AgentRunner _textRunner;

        public Func<BrainToolCall, CancellationToken, Task<bool>> Approval { get; set; }
        public Action<string> Trace { get; set; }
        public IBrainModel Model { get { return _model; } }
        public string BodySummary { get; set; }
        public string UiSummary { get; set; }
        public SelfIterationPolicy Policy { get; set; }
        public Func<BrainToolCall, CancellationToken, Task<bool>> Propose { get; set; }

        public BrainHost(IMemoryStore memory, IBrainModel model, IActionExecutor body = null)
        {
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (body != null) { _body = body; _ownsBody = false; }
            else { _body = new BodyClient(); _ownsBody = true; }
            _textRunner = new AgentRunner(_memory, _model);
        }

        public Task<string> AskAsync(string text, CancellationToken ct)
        {
            _textRunner.BodySummary = BodySummary;
            _textRunner.UiSummary = UiSummary;
            return _textRunner.RunAsync(text, ct);
        }

        public Task<string> RunToolAsync(string text, CancellationToken ct)
        {
            IToolCallingModel tm = _model as IToolCallingModel;
            if (tm == null) throw new InvalidOperationException("current model does not support tool calls");
            var runner = new ToolAgentRunner(_memory, tm, _body);
            runner.Approval = Approval;
            runner.Trace = Trace;
            runner.Policy = Policy;
            runner.Propose = Propose;
            return runner.RunAsync(text, ct);
        }

        public void Dispose()
        {
            if (_ownsBody)
            {
                IDisposable d = _body as IDisposable;
                if (d != null) d.Dispose();
            }
        }
    }
}
