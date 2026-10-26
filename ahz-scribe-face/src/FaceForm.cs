using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.WinForms;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AhzScribe.Brain;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class PulseRow
    {
        public string time { get; set; }
        public string triggerType { get; set; }
        public string action { get; set; }
        public string reason { get; set; }
        public string raw { get; set; }
        public string thought { get; set; }
        public string stateRestore { get; set; }
        public string stateWriteback { get; set; }
        public string auditId { get; set; }
    }

    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class FaceUiHost
    {
        readonly string _dataDir;
        readonly Action<string> _openAudit;
        public FaceUiHost(string dataDir, Action<string> openAudit) { _dataDir = dataDir; _openAudit = openAudit; }

        public void OpenAudit(string auditId) { try { if (_openAudit != null) _openAudit(auditId); } catch { } }

        public string QueryMemories(string filter)
        {
            string dir = Path.Combine(_dataDir, "..");
            string sem = Path.Combine(_dataDir, "memory.jsonl");
            var rows = new List<object>();
            if (!File.Exists(sem)) return "[]";
            foreach (string line in File.ReadLines(sem).Reverse().Take(300).Reverse())
            {
                if (!string.IsNullOrEmpty(filter) && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    using (var doc = System.Text.Json.JsonDocument.Parse(line))
                    {
                        var r = doc.RootElement;
                        rows.Add(new
                        {
                            kind = r.TryGetProperty("Kind", out var k) ? k.GetString() : "",
                            key = r.TryGetProperty("Key", out var key) ? key.GetString() : "",
                            value = r.TryGetProperty("Value", out var v) ? v.GetString() : "",
                            status = r.TryGetProperty("Status", out var st) ? st.GetString() : "",
                            audit = r.TryGetProperty("SourceAuditId", out var a) ? a.GetString() : "",
                            raw = line
                        });
                    }
                }
                catch { }
            }
            return System.Text.Json.JsonSerializer.Serialize(rows);
        }
        public string QueryAudit(string filter)
        {
            string path = Environment.GetEnvironmentVariable("AHZ_AUDIT_PATH");
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AhzScribe", "audit", "audit.jsonl");
            var rows = new List<object>();
            if (!File.Exists(path)) return "[]";
            foreach (string line in File.ReadLines(path).Reverse().Take(200).Reverse())
            {
                if (!string.IsNullOrEmpty(filter) && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    using (var doc = System.Text.Json.JsonDocument.Parse(line))
                    {
                        var r = doc.RootElement;
                        rows.Add(new
                        {
                            ts = r.TryGetProperty("ts", out var ts) ? ts.GetString() : "",
                            span = r.TryGetProperty("span", out var sp) ? sp.GetString() : "",
                            tool = r.TryGetProperty("tool", out var tl) ? tl.GetString() : "",
                            decision = r.TryGetProperty("decision", out var dc) ? dc.GetString() : "",
                            result = r.TryGetProperty("result", out var rs) ? rs.GetString() : "",
                            auditId = r.TryGetProperty("audit_id", out var ai) ? ai.GetString() : "",
                            fallback = r.TryGetProperty("fallback", out var fb) && fb.ValueKind == System.Text.Json.JsonValueKind.True,
                            raw = line
                        });
                    }
                }
                catch { }
            }
            return System.Text.Json.JsonSerializer.Serialize(rows);
        }
        public string QueryPulses(string filter)
        {
            string jsonPath = Path.Combine(_dataDir, "pulse.jsonl");
            if (File.Exists(jsonPath))
            {
                var jr = new List<PulseRow>();
                foreach (string line in File.ReadLines(jsonPath).Reverse().Take(200).Reverse())
                {
                    if (!string.IsNullOrEmpty(filter) && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try
                    {
                        using (var doc = System.Text.Json.JsonDocument.Parse(line))
                        {
                            var r = doc.RootElement;
                            jr.Add(new PulseRow
                            {
                                time = r.TryGetProperty("time", out var t) ? t.GetString() : "",
                                triggerType = r.TryGetProperty("triggerType", out var tt) ? tt.GetString() : "",
                                action = r.TryGetProperty("action", out var a) ? a.GetString() : "",
                                reason = r.TryGetProperty("reason", out var re) ? re.GetString() : "",
                                thought = r.TryGetProperty("thought", out var th) ? th.GetString() : "",
                                stateRestore = r.TryGetProperty("stateRestore", out var sr) ? sr.GetString() : "",
                                stateWriteback = r.TryGetProperty("stateWriteback", out var sw) ? sw.GetString() : "",
                                auditId = r.TryGetProperty("auditId", out var ai) ? ai.GetString() : "",
                                raw = line
                            });
                        }
                    }
                    catch { }
                }
                if (jr.Count > 0) return System.Text.Json.JsonSerializer.Serialize(jr);
            }
            string p = Path.Combine(_dataDir, "pulse.log");
            var rows = new List<PulseRow>();
            if (!File.Exists(p)) return "[]";
            foreach (string line in File.ReadLines(p).Reverse().Take(200).Reverse())
            {
                if (!string.IsNullOrEmpty(filter) && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var row = new PulseRow { raw = line };
                if (line.Length >= 19) row.time = line.Substring(11, 8);
                string rest = line.Length > 19 ? line.Substring(19).Trim() : line;
                string source = rest; string[] parts = new string[0];
                int slash = rest.IndexOf(" / ", StringComparison.Ordinal);
                if (slash >= 0)
                {
                    source = rest.Substring(0, slash).Trim();
                    parts = rest.Substring(slash + 3).Split(new[] { " / " }, StringSplitOptions.None);
                }
                row.triggerType = source;
                if (source == "proactive_message")
                {
                    row.action = parts.Length > 0 ? parts[0] : "";
                    row.triggerType = parts.Length > 2 ? parts[2] : "user_message";
                    row.reason = parts.Length > 2 ? parts[2] : "";
                }
                else if (source == "pulse_model")
                {
                    row.action = parts.Length > 2 ? parts[2] : "none";
                    row.triggerType = "timer";
                    row.reason = parts.Length > 2 ? parts[2] : "";
                }
                else
                {
                    row.action = parts.Length > 1 ? parts[1] : "";
                    row.reason = parts.Length > 2 ? parts[2] : "";
                }
                rows.Add(row);
            }
            return System.Text.Json.JsonSerializer.Serialize(rows);
        }
    }
    public sealed class FaceForm : Form
    {
        readonly RichTextBox _output;
        readonly TextBox _trace;
        readonly TextBox _traceFilter;
        readonly StringBuilder _traceRaw;
        readonly TextBox _memoryView;
        readonly TextBox _statusView;
        readonly TextBox _auditView;
        readonly TextBox _pulseView;
        readonly WebView2 _web;
        readonly Panel _htmlHost;
        string _auditFocus;
        readonly Label _stateFocus, _stateIntents, _stateBody, _statePulse, _stateMood;
        readonly TextBox _taskView;
        readonly Label _topDot, _topIdle, _topFore, _topDisk, _topClock;
        readonly NotifyIcon _tray;
        readonly System.Windows.Forms.Timer _clockTimer;
        readonly UIStateBus _uiBus = new UIStateBus();
        bool _reallyExit;
        readonly TextBox _memoryFilter;
        readonly TextBox _setBase, _setModel, _setWorkspace, _setPipe, _setBodyData;
        readonly CheckBox _setNoProxy;
        readonly TabControl _tabs;
        readonly Label _status;
        readonly Panel _side;
        readonly Button[] _nav;
        readonly TextBox _input;
        readonly Button _send;
        readonly BrainHost _host;
        readonly FacePulseBody _pulseBody;
        readonly BodySensor _sensor;
        readonly System.Threading.Timer _bodyTimer;
        readonly PulseScheduler _pulse;
        readonly PulseQueue _pulseQueue;
        readonly PulseArbiter _arbiter;
        readonly System.Threading.Timer _pulseTimer;
        readonly string _modelName;
        readonly string _sessionPath;
        readonly string _dataDir;
        readonly FaceSettings _settings;
        readonly MemoryStore _memory;

        public FaceForm()
        {
            Text = "Ahz Scribe · face v0";
            Width = 920;
            Height = 640;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 520);
            MinimumSize = new Size(1024, 680);
            BackColor = Theme.BgPrimary;
            ForeColor = Theme.TextPrimary;

            _dataDir = Path.Combine(AppContext.BaseDirectory, "_face-data");
            _sessionPath = Path.Combine(_dataDir, "sessions.jsonl");
            Directory.CreateDirectory(_dataDir);
            _settings = FaceSettings.Load(_dataDir);
            if (!string.IsNullOrEmpty(_settings.BodyPipe)) Environment.SetEnvironmentVariable("AHZ_BODY_PIPE", _settings.BodyPipe);
            if (_settings.ModelNoProxy) Environment.SetEnvironmentVariable("AHZ_MODEL_NOPROXY", "1");
            _memory = new MemoryStore(Path.Combine(_dataDir, "memory.jsonl"));
            _host = new BrainHost(_memory, CreateModel(out _modelName));
            _host.Trace = delegate(string line) { AppendTrace(line); };
            _uiBus.Changed += ApplyUiSnapshot;
            _pulseBody = new FacePulseBody();
            _pulse = new PulseScheduler(_pulseBody, _memory, new FacePulseAudit(AppendTrace, Path.Combine(_dataDir, "pulse.log")),
                new FacePulseSink(OnPulse), new ModelPulseDecider(_memory, _host.Model));
            _pulseQueue = new PulseQueue();
            _arbiter = new PulseArbiter(_pulse, _pulseQueue, delegate { return FaceSession.LoadState(_sessionPath); });
            _pulseTimer = new System.Threading.Timer(delegate { PulseTick(); }, null, 60000, 60000);
            _sensor = new BodySensor(_settings.Workspace);
            _bodyTimer = new System.Threading.Timer(delegate { SampleBody(); }, null, 0, 30000);
            _sensor = new BodySensor(_settings.Workspace);
            _bodyTimer = new System.Threading.Timer(delegate { SampleBody(); }, null, 0, 30000);
            FormClosed += delegate { _pulse.Dispose(); _bodyTimer.Dispose(); _pulseTimer.Dispose(); };
            _host.Policy = new SelfIterationPolicy();
            _host.Propose = delegate(BrainToolCall call, CancellationToken ct)
            {
                string title = call.Args != null && call.Args.ContainsKey("title") ? Convert.ToString(call.Args["title"]) : "自我改造申请";
                string change = call.Args != null && call.Args.ContainsKey("change") ? Convert.ToString(call.Args["change"]) : "";
                bool approved = false;
                using (var dlg = new ApprovalForm(title, change))
                    approved = dlg.ShowDialog(this) == DialogResult.Yes;
                if (approved)
                {
                    string root = string.IsNullOrEmpty(_settings.Workspace) ? (Environment.GetEnvironmentVariable("AHZ_WORKSPACE") ?? "") : _settings.Workspace;
                    if (!string.IsNullOrEmpty(root)) _host.Policy.Begin(root);
                }
                return Task.FromResult(approved);
            };

            _output = new RichTextBox();
            _output.Multiline = true;
            _output.ReadOnly = true;
            _output.ScrollBars = RichTextBoxScrollBars.Vertical;
            _output.Dock = DockStyle.Fill;
            _output.BackColor = Theme.BgPrimary;
            _output.ForeColor = Theme.TextPrimary;
            _output.BorderStyle = BorderStyle.None;
            _output.Font = new Font("Consolas", 10f);

            _trace = new TextBox();
            _trace.Multiline = true;
            _trace.ReadOnly = true;
            _trace.ScrollBars = ScrollBars.Vertical;
            _trace.Dock = DockStyle.Fill;
            _trace.BackColor = Theme.BgPrimary;
            _trace.ForeColor = Theme.TextSecondary;
            _trace.BorderStyle = BorderStyle.None;
            _trace.Font = new Font("Consolas", 9f);

            _traceRaw = new StringBuilder();
            _traceFilter = new TextBox();
            _traceFilter.Dock = DockStyle.Top;
            _traceFilter.BackColor = Theme.BgSecondary;
            _traceFilter.ForeColor = Theme.TextPrimary;
            _traceFilter.BorderStyle = BorderStyle.FixedSingle;
            _traceFilter.PlaceholderText = "过滤轨迹...";
            _traceFilter.TextChanged += delegate { RefreshTrace(); };

            _memoryView = new TextBox();
            _memoryView.Multiline = true;
            _memoryView.ReadOnly = true;
            _memoryView.ScrollBars = ScrollBars.Vertical;
            _memoryView.Dock = DockStyle.Fill;
            _memoryView.BackColor = Theme.BgPrimary;
            _memoryView.ForeColor = Theme.TextSecondary;
            _memoryView.BorderStyle = BorderStyle.None;
            _memoryView.Font = new Font("Consolas", 9f);

            _statusView = new TextBox();
            _statusView.Multiline = true;
            _statusView.ReadOnly = true;
            _statusView.ScrollBars = ScrollBars.Vertical;
            _statusView.Dock = DockStyle.Fill;
            _statusView.BackColor = Theme.BgPrimary;
            _statusView.ForeColor = Theme.TextPrimary;
            _statusView.BorderStyle = BorderStyle.None;
            _statusView.Font = new Font("Consolas", 9f);

            _auditView = new TextBox();
            _auditView.Multiline = true;
            _auditView.ReadOnly = true;
            _auditView.ScrollBars = ScrollBars.Vertical;
            _auditView.Dock = DockStyle.Fill;
            _auditView.BackColor = Theme.BgPrimary;
            _auditView.ForeColor = Theme.TextPrimary;
            _auditView.BorderStyle = BorderStyle.None;
            _auditView.Font = new Font("Consolas", 8.5f);

            _pulseView = new TextBox();
            _pulseView.Multiline = true;
            _pulseView.ReadOnly = true;
            _pulseView.ScrollBars = ScrollBars.Vertical;
            _pulseView.Dock = DockStyle.Fill;
            _pulseView.BackColor = Theme.BgPrimary;
            _pulseView.ForeColor = Theme.TextPrimary;
            _pulseView.BorderStyle = BorderStyle.None;
            _pulseView.Font = new Font("Consolas", 8.5f);
            _web = new WebView2();
            _web.Dock = DockStyle.Fill;
            _web.BackColor = Theme.BgPrimary;
            _web.DefaultBackgroundColor = Theme.BgPrimary;
            _htmlHost = new Panel();
            _htmlHost.Dock = DockStyle.Fill;
            _htmlHost.Visible = false;
            _htmlHost.BackColor = Theme.BgPrimary;
            _htmlHost.Controls.Add(_web);
            _web.CoreWebView2InitializationCompleted += delegate(object s, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(_dataDir, "webview.log"), "init success=" + e.IsSuccess + " " + e.InitializationException + "\n"); } catch { }
                if (!e.IsSuccess)
                {
                    _htmlHost.Visible = false;
                    _pulseView.Visible = true;
                    _pulseView.Text = "HTML 面板不可用，已切换为只读文本模式。\n" + e.InitializationException;
                }
            };
            _web.NavigationCompleted += delegate(object s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) { try { File.AppendAllText(Path.Combine(_dataDir, "webview.log"), "nav success=" + e.IsSuccess + " " + e.WebErrorStatus + "\n"); } catch { } };
            _taskView = new TextBox();
            _taskView.Multiline = true;
            _taskView.ReadOnly = true;
            _taskView.Dock = DockStyle.Fill;
            _taskView.BackColor = Theme.BgPrimary;
            _taskView.ForeColor = Theme.TextPrimary;
            _taskView.BorderStyle = BorderStyle.None;
            _taskView.Font = new Font("Consolas", 9f);

            _memoryFilter = new TextBox();
            _memoryFilter.Dock = DockStyle.Top;
            _memoryFilter.BackColor = Theme.BgSecondary;
            _memoryFilter.ForeColor = Theme.TextPrimary;
            _memoryFilter.BorderStyle = BorderStyle.FixedSingle;
            _memoryFilter.PlaceholderText = "搜索记忆...";
            _memoryFilter.TextChanged += delegate { RenderMemory(); };

            _setBase = MakeSettingBox(_settings.BaseUrl);
            _setModel = MakeSettingBox(_settings.Model);
            _setWorkspace = MakeSettingBox(_settings.Workspace);
            _setPipe = MakeSettingBox(_settings.BodyPipe);
            _setBodyData = MakeSettingBox(_settings.BodyData);
            _setBodyData.PlaceholderText = "body 数据目录（读审计用）";
            _setNoProxy = new CheckBox();
            _setNoProxy.Text = "模型直连（不使用系统代理）";
            _setNoProxy.ForeColor = Theme.TextPrimary;
            _setNoProxy.Checked = _settings.ModelNoProxy;
            _setWorkspace.PlaceholderText = "工作区路径；留空则不能自我迭代";

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Microsoft YaHei UI", 9f);
            _tabs.Appearance = TabAppearance.FlatButtons;
            _tabs.ItemSize = new Size(0, 1);
            _tabs.SizeMode = TabSizeMode.Fixed;
            var chatPage = new TabPage("对话");
            chatPage.BackColor = Theme.BgPrimary;
            chatPage.Controls.Add(_output);
            var tracePage = new TabPage("轨迹");
            tracePage.BackColor = Theme.BgPrimary;
            var tracePanel = new Panel();
            tracePanel.Dock = DockStyle.Fill;
            tracePanel.Controls.Add(_trace);
            tracePanel.Controls.Add(_traceFilter);
            tracePage.Controls.Add(tracePanel);
            var pulsePage = new TabPage("脉冲");
            pulsePage.BackColor = Theme.BgPrimary;
            var pulsePanel = new Panel();
            pulsePanel.Dock = DockStyle.Fill;
            var pulseRefresh = new Button();
            pulseRefresh.Text = "刷新";
            pulseRefresh.Dock = DockStyle.Top;
            pulseRefresh.Height = 28;
            pulseRefresh.Click += delegate { RenderPulse(); };
            pulsePanel.Controls.Add(pulseRefresh);
            _pulseView.Visible = false;
            pulsePanel.Controls.Add(_pulseView);
            pulsePage.Controls.Add(pulsePanel);
            var memoryPage = new TabPage("记忆");
            memoryPage.BackColor = Theme.BgPrimary;
            var memoryPanel = new Panel();
            memoryPanel.Dock = DockStyle.Fill;
            memoryPanel.Controls.Add(_memoryView);
            memoryPanel.Controls.Add(_memoryFilter);
            memoryPage.Controls.Add(memoryPanel);
            var statusPage = new TabPage("状态");
            statusPage.BackColor = Theme.BgPrimary;
            statusPage.Controls.Add(_statusView);
            var auditPage = new TabPage("审计");
            auditPage.BackColor = Theme.BgPrimary;
            var auditPanel = new Panel();
            auditPanel.Dock = DockStyle.Fill;
            var auditRefresh = new Button();
            auditRefresh.Text = "刷新";
            auditRefresh.Dock = DockStyle.Top;
            auditRefresh.Height = 28;
            auditRefresh.Click += delegate { RenderAudit(); };
            auditPanel.Controls.Add(_auditView);
            auditPanel.Controls.Add(auditRefresh);
            auditPage.Controls.Add(auditPanel);
            var settingsPage = new TabPage("设置");
            settingsPage.BackColor = Theme.BgPrimary;
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(18);
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowCount = 7;
            for (int i = 0; i < 4; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            table.Controls.Add(new Label { Text = "模型 Base URL", ForeColor = Theme.TextPrimary, AutoSize = true }, 0, 0);
            table.Controls.Add(_setBase, 1, 0);
            table.Controls.Add(new Label { Text = "模型名", ForeColor = Theme.TextPrimary, AutoSize = true }, 0, 1);
            table.Controls.Add(_setModel, 1, 1);
            table.Controls.Add(new Label { Text = "工作区", ForeColor = Theme.TextPrimary, AutoSize = true }, 0, 2);
            table.Controls.Add(_setWorkspace, 1, 2);
            table.Controls.Add(new Label { Text = "body 管道", ForeColor = Theme.TextPrimary, AutoSize = true }, 0, 3);
            table.Controls.Add(_setPipe, 1, 3);
            var save = new Button();
            save.Text = "保存（重启 face 生效）";
            save.Width = 220;
            save.FlatStyle = FlatStyle.Flat;
            save.FlatAppearance.BorderSize = 0;
            save.BackColor = Theme.StateInfo;
            save.ForeColor = Theme.TextPrimary;
            save.Click += delegate { SaveSettings(); };
            table.Controls.Add(_setNoProxy, 1, 4);
            table.Controls.Add(new Label { Text = "body 数据目录", ForeColor = Theme.TextPrimary, AutoSize = true }, 0, 5);
            table.Controls.Add(_setBodyData, 1, 5);
            table.Controls.Add(save, 1, 6);
            settingsPage.Controls.Add(table);
            var taskPage = new TabPage("任务");
            taskPage.BackColor = Theme.BgPrimary;
            taskPage.Controls.Add(_taskView);
            _tabs.TabPages.Add(chatPage);
            _tabs.TabPages.Add(pulsePage);
            _tabs.TabPages.Add(memoryPage);
            _tabs.TabPages.Add(auditPage);
            _tabs.TabPages.Add(taskPage);
            _tabs.TabPages.Add(settingsPage);
            _side = new Panel();
            _side.Dock = DockStyle.Left;
            _side.Width = 260;
            _side.BackColor = Theme.BgBase;
            var statePanel = new Panel();
            statePanel.Dock = DockStyle.Bottom;
            statePanel.Height = 220;
            statePanel.BackColor = Theme.BgSecondary;
            statePanel.Padding = new Padding(10);
            _stateFocus = MakeStateLabel();
            _stateIntents = MakeStateLabel();
            _stateBody = MakeStateLabel();
            _statePulse = MakeStateLabel();
            _stateMood = MakeStateLabel();
            statePanel.Controls.Add(_stateMood);
            statePanel.Controls.Add(_statePulse);
            statePanel.Controls.Add(_stateBody);
            statePanel.Controls.Add(_stateIntents);
            statePanel.Controls.Add(_stateFocus);
            var brand = new Label();
            brand.Text = "AHZ\nSCRIBE";
            brand.Dock = DockStyle.Top;
            brand.Height = 72;
            brand.Padding = new Padding(16, 14, 0, 0);
            brand.Font = new Font("Consolas", 13f, FontStyle.Bold);
            brand.ForeColor = Theme.StateInfo;
            _side.Controls.Add(brand);
            string[] navNames = { "对话", "脉冲", "记忆", "审计", "任务", "设置" };
            _nav = new Button[navNames.Length];
            for (int i = navNames.Length - 1; i >= 0; i--)
            {
                int index = i;
                var b = new Button();
                b.Text = navNames[i];
                b.Dock = DockStyle.Top;
                b.Height = 46;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = Theme.BgBase;
                b.ForeColor = Theme.TextSecondary;
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.Padding = new Padding(18, 0, 0, 0);
                b.Click += delegate { _tabs.SelectedIndex = index; UpdateNav(); };
                _side.Controls.Add(b);
                _nav[i] = b;
            }
            var search = new Button();
            search.Text = "搜索对话";
            search.Dock = DockStyle.Bottom;
            search.Height = 46;
            search.FlatStyle = FlatStyle.Flat;
            search.FlatAppearance.BorderSize = 0;
            search.BackColor = Theme.BgBase;
            search.ForeColor = Theme.TextSecondary;
            search.TextAlign = ContentAlignment.MiddleLeft;
            search.Padding = new Padding(18, 0, 0, 0);
            search.Click += delegate
            {
                using (var f = new HistorySearchForm(_sessionPath))
                    if (f.ShowDialog(this) == DialogResult.OK && f.SelectedMessage != null) JumpTo(f.SelectedMessage);
            };
            _side.Controls.Add(search);
            _side.Controls.Add(statePanel);
            _side.Controls.Add(brand);
            _tabs.SelectedIndexChanged += delegate { UpdateNav(); ShowHtmlTab(); };
            UpdateNav();
            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 120;
            bottom.BackColor = Theme.BgPrimary;

            _input = new TextBox();
            _input.Dock = DockStyle.Fill;
            _input.Multiline = true;
            _input.PlaceholderText = "输入问题；/tool 前缀让模型使用工具";
            _input.ScrollBars = ScrollBars.Vertical;
            _input.Font = new Font("Microsoft YaHei UI", 10f);
            _input.BackColor = Theme.BgSecondary;
            _input.ForeColor = Theme.TextPrimary;
            _input.BorderStyle = BorderStyle.FixedSingle;

            _send = new Button();
            _send.Text = "发送";
            _send.Dock = DockStyle.Right;
            _send.Width = 88;
            _send.FlatStyle = FlatStyle.Flat;
            _send.FlatAppearance.BorderSize = 0;
            _send.BackColor = Theme.StateInfo;
            _send.ForeColor = Theme.TextPrimary;
            _send.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            _send.Click += delegate { SendAsync(); };
            _input.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; SendAsync(); }
            };

            _status = new Label();
            _status.Dock = DockStyle.Top;
            _status.Height = 22;
            _status.BackColor = Theme.BgPrimary;
            _status.ForeColor = Theme.TextSecondary;
            _status.Font = new Font("Consolas", 8.5f);
            _status.Padding = new Padding(10, 3, 4, 0);
            _status.Text = "episode -   memory 0   body " + (Environment.GetEnvironmentVariable("AHZ_BODY_PIPE") ?? "ahz-scribe-body");
            bottom.Controls.Add(_status);
            var pad = new Panel();
            pad.Dock = DockStyle.Fill;
            pad.Padding = new Padding(10, 32, 8, 8);
            pad.Controls.Add(_input);
            bottom.Controls.Add(pad);
            bottom.Controls.Add(_send);
            var stopBtn = new Button();
            stopBtn.Text = "■ 急停";
            stopBtn.Dock = DockStyle.Right;
            stopBtn.Width = 84;
            stopBtn.FlatStyle = FlatStyle.Flat;
            stopBtn.FlatAppearance.BorderSize = 0;
            stopBtn.BackColor = Theme.DangerButtonBg;
            stopBtn.ForeColor = Theme.DangerButtonFg;
            stopBtn.Click += delegate { EmergencyStop(); };
            var auditBtn = new Button();
            auditBtn.Text = "审计";
            auditBtn.Dock = DockStyle.Right;
            auditBtn.Width = 64;
            auditBtn.FlatStyle = FlatStyle.Flat;
            auditBtn.FlatAppearance.BorderSize = 0;
            auditBtn.BackColor = Theme.BgTertiary;
            auditBtn.ForeColor = Theme.TextPrimary;
            auditBtn.Click += delegate { _tabs.SelectedIndex = 3; UpdateNav(); };
            bottom.Controls.Add(stopBtn);
            bottom.Controls.Add(auditBtn);


            var top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 32;
            top.BackColor = Theme.BgPrimary;
            _topDot = new Label(); _topDot.Text = "●"; _topDot.AutoSize = false; _topDot.Width = 22; _topDot.Dock = DockStyle.Left; _topDot.TextAlign = ContentAlignment.MiddleCenter; _topDot.ForeColor = Theme.StateInfo; _topDot.Font = new Font("Segoe UI", 10f);
            _topIdle = new Label(); _topIdle.Dock = DockStyle.Left; _topIdle.Width = 140; _topIdle.TextAlign = ContentAlignment.MiddleLeft; _topIdle.ForeColor = Theme.TextPrimary; _topIdle.Font = new Font("Microsoft YaHei UI", 9f);
            _topFore = new Label(); _topFore.Dock = DockStyle.Left; _topFore.Width = 300; _topFore.TextAlign = ContentAlignment.MiddleLeft; _topFore.ForeColor = Theme.TextPrimary; _topFore.Font = new Font("Microsoft YaHei UI", 9f);
            _topDisk = new Label(); _topDisk.Dock = DockStyle.Right; _topDisk.Width = 150; _topDisk.TextAlign = ContentAlignment.MiddleRight; _topDisk.ForeColor = Theme.TextPrimary; _topDisk.Font = new Font("Consolas", 9f);
            _topClock = new Label(); _topClock.Dock = DockStyle.Right; _topClock.Width = 90; _topClock.TextAlign = ContentAlignment.MiddleRight; _topClock.ForeColor = Theme.TextPrimary; _topClock.Font = new Font("Consolas", 9f);
            top.Controls.Add(_topIdle); top.Controls.Add(_topFore); top.Controls.Add(_topDisk); top.Controls.Add(_topClock); top.Controls.Add(_topDot);

            var main = new Panel();
            main.Dock = DockStyle.Fill;
            var content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Controls.Add(_htmlHost);
            content.Controls.Add(_tabs);
            main.Controls.Add(content);
            main.Controls.Add(_side);
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.Controls.Add(top, 0, 0);
            root.Controls.Add(main, 0, 1);
            root.Controls.Add(bottom, 0, 2);
            Controls.Add(root);

            _clockTimer = new System.Windows.Forms.Timer();
            _clockTimer.Interval = 1000;
            _clockTimer.Tick += delegate { _topClock.Text = DateTime.Now.ToString("HH:mm:ss"); };
            _clockTimer.Start();

            _tray = new NotifyIcon();
            _tray.Icon = SystemIcons.Application;
            _tray.Text = "Ahz Scribe";
            _tray.Visible = true;
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("打开 Ahz Scribe", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("查看审计", null, delegate { Show(); _tabs.SelectedIndex = 3; UpdateNav(); });
            trayMenu.Items.Add("急停", null, delegate { EmergencyStop(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("退出", null, delegate { _reallyExit = true; _tray.Visible = false; Close(); });
            _tray.ContextMenuStrip = trayMenu;
            _tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (!_reallyExit) { e.Cancel = true; Hide(); }
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) Hide(); };
            top.BringToFront();
            bottom.BringToFront();

            Append("face v0 · model=" + _modelName);
            Append("输入问题；/tool 前缀让模型使用工具。危险操作会弹确认框。");
            List<FaceMessage> history = FaceSession.Load(_sessionPath, 200);
            if (history.Count == 0) Append("这是你的连续时间线。没有 /new，也没有会话列表；直接说话就行。");
            foreach (FaceMessage old in history)
            {
                if (old.Kind == "tool") AppendTrace(old.Text);
                else AppendHistory(old);
            }
            RememberOldEpisodes(history);
            RenderMemory();
            UpdateStatus();
            FaceState state = FaceSession.LoadState(_sessionPath);
            if (state != null) UpdateTitle(state.CurrentFocus);
            UpdateStatus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                _output.Visible = true;
                _output.BringToFront();
                string dbg = Path.Combine(Path.GetDirectoryName(_sessionPath), "ui-debug.log");
                File.AppendAllText(dbg, "onshown vis=" + _output.Visible + " size=" + _output.Width + "x" + _output.Height + " parent=" + (_output.Parent == null ? "null" : _output.Parent.GetType().Name) + " textlen=" + _output.TextLength + " handle=" + _output.IsHandleCreated + " fore=" + _output.ForeColor.ToString() + " back=" + _output.BackColor.ToString() + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
        IBrainModel CreateModel(out string name)
        {
            string key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            string baseUrl = Environment.GetEnvironmentVariable("AHZ_MODEL_BASE") ?? "https://api.deepseek.com";
            string model = Environment.GetEnvironmentVariable("AHZ_MODEL_NAME") ?? "deepseek-v4-pro";
            name = model;
            if (string.IsNullOrEmpty(key)) return new EchoModel();
            return new OpenAiToolModel(new ModelOptions { BaseUrl = baseUrl, Model = model, ApiKey = key, MaxTokens = 4096 });
        }

        void Append(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Append), text); return; }
            try { string dbg = Path.Combine(Path.GetDirectoryName(_sessionPath), "ui-debug.log"); File.AppendAllText(dbg, text + " | len=" + _output.TextLength + " vis=" + _output.Visible + " size=" + _output.Width + "x" + _output.Height + "\n", new UTF8Encoding(false)); } catch { }
            _output.AppendText(text + Environment.NewLine + Environment.NewLine);
            _output.SelectionStart = _output.TextLength;
            _output.ScrollToCaret();
        }

        async void RememberOldEpisodes(List<FaceMessage> history)
        {
            try
            {
                await Task.Run(async delegate
                {
                    FaceState st = FaceSession.LoadState(_sessionPath);
                    string current = st == null ? "" : st.CurrentEpisode;
                    var known = new HashSet<string>(_memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult()
                        .Select(x => x.Key ?? ""), StringComparer.OrdinalIgnoreCase);
                    bool realModel = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
                    foreach (var g in history.Where(x => !string.IsNullOrEmpty(x.Episode) && x.Episode != current)
                        .GroupBy(x => x.Episode))
                    {
                        if (known.Contains("episode:" + g.Key)) continue;
                        var recent = g.Skip(Math.Max(0, g.Count() - 8)).ToList();
                        string fallback = string.Join("\n", recent.Select(x => (x.Role ?? "") + ": " + Cut(x.Text, 200)));
                        string summary = fallback;
                        if (realModel)
                        {
                            try
                            {
                                string prompt = "请把下面这段旧的对话压缩成 1-3 句 episodic memory，只写事实、承诺和偏好，不要寒暄：\n" + fallback;
                                string generated = await _host.AskAsync(prompt, CancellationToken.None).ConfigureAwait(false);
                                if (!string.IsNullOrWhiteSpace(generated)) summary = generated.Trim();
                            }
                            catch { }
                        }
                        string sourceAudit = FaceAudit.Append(_dataDir, "memory", "extract", "allowed", "episode " + g.Key, "episode summary");
                        _memory.RememberAsync(new MemoryItem
                        {
                            Kind = "episodic",
                            Key = "episode:" + g.Key,
                            Value = summary,
                            Summary = "episode " + g.Key,
                            SourceAuditId = sourceAudit,
                            Status = "active"
                        }, CancellationToken.None).GetAwaiter().GetResult();
                        try
                        {
                            var extractor = new MemoryExtractor(_host.Model);
                            List<MemoryItem> extracted = await extractor.ExtractAsync(fallback, sourceAudit, CancellationToken.None).ConfigureAwait(false);
                            foreach (MemoryItem m in extracted)
                                _memory.RememberAsync(m, CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                }).ConfigureAwait(false);
            }
            catch { }
        }

        static string StopPath()
        {
            string f = Environment.GetEnvironmentVariable("AHZ_STOP_FILE");
            if (!string.IsNullOrEmpty(f)) return f;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AhzScribe", "state", "EMERGENCY_STOP");
        }
        static string Cut(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
        void AppendHistory(FaceMessage m)
        {
            string role = m.Role ?? "";
            _output.SelectionColor = role == "你" ? Theme.StateInfo : Theme.TextPrimary;
            _output.AppendText(role + "：");
            _output.SelectionColor = Theme.TextPrimary;
            _output.AppendText((m.Text ?? "") + Environment.NewLine + Environment.NewLine);
        }
        void AppendChat(string role, string text, string kind)
        {
            FaceSession.Append(_sessionPath, new FaceMessage { Role = role, Text = text, Kind = kind });
            _output.SelectionColor = role == "你" ? Theme.StateInfo : Theme.TextPrimary;
            _output.AppendText(role + "：");
            _output.SelectionColor = Theme.TextPrimary;
            _output.AppendText((text ?? "") + Environment.NewLine + Environment.NewLine);
            _output.SelectionStart = _output.TextLength;
            _output.ScrollToCaret();
        }
        void AppendTrace(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendTrace), text); return; }
            FaceSession.Append(_sessionPath, new FaceMessage { Role = "工具", Text = text, Kind = "tool" });
            _traceRaw.AppendLine("[工具] " + text);
            RefreshTrace();
        }
        void RefreshTrace()
        {
            string filter = _traceFilter == null ? "" : _traceFilter.Text.Trim();
            string raw = _traceRaw.ToString();
            if (filter.Length > 0)
            {
                var sb = new StringBuilder();
                foreach (string line in raw.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                    if (line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) sb.AppendLine(line);
                raw = sb.ToString();
            }
            _trace.Text = raw;
            _trace.SelectionStart = _trace.TextLength;
            _trace.ScrollToCaret();
        }
        void RenderMemory()
        {
            try
            {
                List<MemoryItem> all = _memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult();
                var sb = new System.Text.StringBuilder();
                string filter = _memoryFilter == null ? "" : _memoryFilter.Text.Trim();
                if (filter.Length > 0)
                    all = all.Where(x => ((x.Key ?? "") + " " + (x.Value ?? "") + " " + (x.Kind ?? "")).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                foreach (MemoryItem m in all.Skip(Math.Max(0, all.Count - 30)))
                    sb.AppendLine("[" + (m.Kind ?? "") + "] " + (m.Key ?? "") + " = " + Cut(m.Value, 200));
                _memoryView.Text = sb.ToString();
            }
            catch { }
        }
        TextBox MakeSettingBox(string value)
        {
            var b = new TextBox();
            b.Text = value ?? "";
            b.Dock = DockStyle.Fill;
            b.BackColor = Theme.BgSecondary;
            b.ForeColor = Theme.TextPrimary;
            b.BorderStyle = BorderStyle.FixedSingle;
            b.Font = new Font("Consolas", 9.5f);
            b.Margin = new Padding(0, 4, 0, 4);
            return b;
        }
        bool AllowProactive()
        {
            FaceState st = FaceSession.LoadState(_sessionPath);
            if (st == null) return true;
            DateTime now = DateTime.Now;
            if (now.Hour < 8 || now.Hour >= 23) return false;
            DateTime paused;
            if (DateTime.TryParse(st.ProactivePausedUntil, out paused) && paused > now) return false;
            string today = now.ToString("yyyy-MM-dd");
            if (st.LastProactiveDate != today) { st.LastProactiveDate = today; st.ProactiveCount = 0; }
            if (st.ProactiveCount >= 2) return false;
            st.ProactiveCount++;
            st.LastProactiveAt = now.ToString("o");
            st.UnansweredProactive++;
            if (st.UnansweredProactive >= 2) st.ProactivePausedUntil = now.AddHours(24).ToString("o");
            FaceSession.SaveState(_sessionPath, st);
            return true;
        }
        void RunDrive()
        {
            try
            {
                FaceState st = FaceSession.LoadState(_sessionPath);
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                if (st != null && st.LastDriveDate == today) { AppendTrace("[drive] already today"); return; }
                List<MemoryItem> all = _memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult();
                DriveGoal g = DriveEngine.Pick(all);
                if (g == null) { AppendTrace("[drive] no goal"); return; }
                FaceSession.AddIntent(_sessionPath, g.Text, "", "drive");
                FaceState now = FaceSession.LoadState(_sessionPath);
                if (now != null) { now.LastDriveDate = today; FaceSession.SaveState(_sessionPath, now); }
                FaceAudit.Append(_dataDir, "drive", g.Type, "proposed", g.Text, g.Reason);
                AppendTrace("[drive] " + g.Type + " score=" + g.Score.ToString("F2") + " " + g.Text);
            }
            catch { }
        }
        void PulseTick()
        {
            try
            {
                FaceState st = FaceSession.LoadState(_sessionPath);
                if (st != null && !string.IsNullOrEmpty(st.ActiveTask)) return;
                DateTime lastCheck = DateTime.MinValue;
                if (st != null && !string.IsNullOrEmpty(st.LastIntentCheck)) DateTime.TryParse(st.LastIntentCheck, out lastCheck);
                DateTime now = DateTime.Now;
                bool checkDue = (now - lastCheck).TotalMinutes >= 10;
                bool hasOpen = st != null && st.OpenIntents != null && st.OpenIntents.Any(x => x.Status == "open" || x.Status == "monitoring");
                bool hasDue = hasOpen && st.OpenIntents.Any(x =>
                {
                    if (x.Status != "open" && x.Status != "monitoring") return false;
                    DateTime dueAt;
                    return string.IsNullOrEmpty(x.DueAt) || (DateTime.TryParse(x.DueAt, out dueAt) && dueAt <= now);
                });
                bool due = checkDue && hasDue;
                if (checkDue && st != null) { st.LastIntentCheck = now.ToString("o"); FaceSession.SaveState(_sessionPath, st); }
                bool idle = _sensor != null && _sensor.Snapshot().IdleSeconds >= 1800;
                string type = due ? "intent_due" : (idle && hasOpen ? "body_event" : "timer");
                FaceSession.SetPulseTrigger(_sessionPath, type, "scheduled");
                PulseDecision d = _arbiter.EnqueueAsync(new PulseTrigger { Type = type, Payload = "scheduled" }, CancellationToken.None).GetAwaiter().GetResult();
                WritePulseDetail(type, d, st);
            }
            catch { }
        }
        void WritePulseDetail(string type, PulseDecision d, FaceState st)
        {
            try
            {
                if (d == null) return;
                var rec = new
                {
                    time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    triggerType = type,
                    action = d.Action ?? "none",
                    reason = d.Reason ?? "",
                    summary = d.Text ?? "",
                    thought = d.Thought ?? "",
                    stateRestore = "focus=" + (st == null ? "" : st.CurrentFocus) + "; idle=" + (_sensor == null ? 0 : _sensor.Snapshot().IdleSeconds),
                    stateWriteback = "last_pulse_result=" + (d.Action ?? "none"),
                    auditId = ""
                };
                File.AppendAllText(Path.Combine(_dataDir, "pulse.jsonl"), JsonSerializer.Serialize(rec) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }
        void SampleBody()
        {
            try
            {
                BodyStatus s = _sensor.Snapshot();
                FaceState st = FaceSession.LoadState(_sessionPath);
                s.ActiveTask = st != null && !string.IsNullOrEmpty(st.ActiveTask);
                string path = Path.Combine(_dataDir, "body_state.json");
                File.WriteAllText(path, JsonSerializer.Serialize(s), new UTF8Encoding(false));
                _host.BodySummary = _sensor.Summary(s);
                UpdateStatus();
            }
            catch { }
        }
        void OnPulse(PulseEvent evt)
        {
            if (InvokeRequired) { BeginInvoke(new Action<PulseEvent>(OnPulse), evt); return; }
            List<MemoryItem> pm = _memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (!ProactivePolicy.ReferencesMemory(evt.Summary ?? "", pm)) { FaceAudit.Append(_dataDir, "proactive", "message", "blocked", evt.Summary ?? "", "no_memory_reference"); return; }
            if (!AllowProactive()) { FaceAudit.Append(_dataDir, "proactive", "message", "blocked", "", "policy"); return; }
            if (!string.IsNullOrEmpty(evt.IntentId)) FaceSession.CloseIntent(_sessionPath, evt.IntentId, "done");
            AppendChat("Ahz", evt.Summary ?? "提醒", "chat");
        }
        void JumpTo(FaceMessage m)
        {
            _tabs.SelectedIndex = 0;
            string needle = (m.Role ?? "") + "：" + (m.Text ?? "");
            int i = _output.Find(needle, RichTextBoxFinds.None);
            if (i < 0 && !string.IsNullOrEmpty(m.Text))
                i = _output.Find(m.Text, RichTextBoxFinds.None);
            if (i >= 0)
            {
                _output.Select(i, needle.Length);
                _output.ScrollToCaret();
                _output.Focus();
            }
        }
        void RenderAudit()
        {
            try
            {
                var sb = new StringBuilder();
                string body = AuditPath();
                if (File.Exists(body)) foreach (string l in File.ReadLines(body).Reverse().Take(80).Reverse()) sb.AppendLine(l);
                string face = FaceAudit.PathFor(_dataDir);
                if (File.Exists(face)) foreach (string l in File.ReadLines(face).Reverse().Take(40).Reverse()) sb.AppendLine(l);
                _auditView.Text = sb.Length == 0 ? "没有审计记录" : sb.ToString();
            }
            catch (Exception ex) { _auditView.Text = ex.Message; }
        }
        static Label MakeStateLabel()
        {
            var l = new Label();
            l.Dock = DockStyle.Top;
            l.Height = 38;
            l.ForeColor = Theme.TextPrimary;
            l.Font = new Font("Microsoft YaHei UI", 9f);
            l.AutoEllipsis = true;
            return l;
        }
        void ApplyUiSnapshot(UiSnapshot s)
        {
            if (s == null) return;
            if (InvokeRequired) { BeginInvoke(new Action<UiSnapshot>(ApplyUiSnapshot), s); return; }
            BodyStatus b = s.Body ?? new BodyStatus();
            FaceState st = s.State;
            if (_topIdle != null)
            {
                string fore = b.ForegroundApp ?? "";
                if (fore.Length > 26) fore = fore.Substring(0, 26) + "…";
                _topIdle.Text = " 空闲 " + (b.IdleSeconds / 60) + " 分";
                _topFore.Text = " 前台 " + fore;
                _topDisk.Text = "磁盘 " + b.DiskFreeGb.ToString("F0") + "G ";
                string em = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AhzScribe", "state", "EMERGENCY_STOP");
                _topDot.ForeColor = File.Exists(em) ? Theme.StateDanger : (b.AgentProcessAlive ? Theme.StateInfo : Theme.StateNeutral);
                if (_tray != null) _tray.Text = "Ahz Scribe · " + (b.AgentProcessAlive ? "运行中" : "休眠");
            }
            if (_stateFocus != null)
            {
                _stateFocus.Text = "当前焦点\n" + (st == null || string.IsNullOrEmpty(st.CurrentFocus) ? "—" : st.CurrentFocus);
                int oi = st == null || st.OpenIntents == null ? 0 : st.OpenIntents.Count(x => x.Status == "open" || x.Status == "monitoring");
                string first = st == null || st.OpenIntents == null ? "" : st.OpenIntents.Where(x => x.Status == "open" || x.Status == "monitoring").Select(x => x.Text).FirstOrDefault();
                _stateIntents.Text = "未完成意图 " + oi + (string.IsNullOrEmpty(first) ? "" : "\n" + first);
                _stateBody.Text = "身体\n空闲 " + (b.IdleSeconds / 60) + " 分 · " + (b.NetworkOnline ? "在线" : "离线");
                _statePulse.Text = "最近脉冲\n" + (st == null || st.LastPulseTrigger == null ? "—" : st.LastPulseTrigger.Type + " " + st.LastPulseTrigger.ReceivedAt);
                string mood = st != null && !string.IsNullOrEmpty(st.ActiveTask) ? "专注" : (st != null && string.Equals(st.LastPulseResult, "none", StringComparison.OrdinalIgnoreCase) ? "安静" : "平静");
                _stateMood.Text = "情绪\n" + mood;
            }
        }
        void ShowHtmlTab()
        {
            if (_tabs.SelectedIndex >= 1 && _tabs.SelectedIndex <= 3)
            {
                _htmlHost.Visible = true;
                _htmlHost.BringToFront();
                if (_tabs.SelectedIndex == 1) RenderPulse();
                else if (_tabs.SelectedIndex == 2) RenderMemoryHtml();
                else RenderAuditHtml();
            }
            else _htmlHost.Visible = false;
        }

        string HtmlPage(string title, string body)
        {
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset='utf-8'><style>");
            sb.Append(":root{--bg:").Append(Css(Theme.BgPrimary)).Append(";--bg2:").Append(Css(Theme.BgSecondary)).Append(";--bg3:").Append(Css(Theme.BgTertiary)).Append(";--line:").Append(Css(Theme.BorderSubtle)).Append(";--text:").Append(Css(Theme.TextPrimary)).Append(";--muted:").Append(Css(Theme.TextSecondary)).Append(";--weak:").Append(Css(Theme.TextTertiary)).Append(";--info:").Append(Css(Theme.StateInfo)).Append(";--success:").Append(Css(Theme.StateSuccess)).Append(";--danger:").Append(Css(Theme.StateDanger)).Append(";--warning:").Append(Css(Theme.StateWarning)).Append(";}");
            sb.Append("body{margin:0;padding:12px;background:var(--bg);color:var(--text);font:13px 'Microsoft YaHei UI','Segoe UI',sans-serif;}h2{font:600 14px 'Microsoft YaHei UI';color:var(--muted);margin:0 0 10px;}table{width:100%;border-collapse:collapse;font:12px Consolas,monospace;}th{text-align:left;color:var(--muted);font-weight:normal;background:var(--bg3);padding:6px;}td{border-bottom:1px solid var(--line);padding:6px;vertical-align:top;}tr:hover{background:var(--bg2);}pre{white-space:pre-wrap;word-break:break-all;font:12px Consolas,monospace;} .warn{color:var(--warning);} .danger{color:var(--danger);} .ok{color:var(--success);} .empty{color:var(--weak);font-style:italic;}");
            sb.Append("</style></head><body><h2>").Append(title).Append("</h2>").Append(body).Append("</body></html>");
            return sb.ToString();
        }

        async void RenderMemoryHtml()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("<!doctype html><html><head><meta charset='utf-8'><style>");
                sb.Append(":root{--bg:").Append(Css(Theme.BgPrimary)).Append(";--bg2:").Append(Css(Theme.BgSecondary)).Append(";--bg3:").Append(Css(Theme.BgTertiary)).Append(";--line:").Append(Css(Theme.BorderSubtle)).Append(";--text:").Append(Css(Theme.TextPrimary)).Append(";--muted:").Append(Css(Theme.TextSecondary)).Append(";--weak:").Append(Css(Theme.TextTertiary)).Append(";--info:").Append(Css(Theme.StateInfo)).Append(";--warning:").Append(Css(Theme.StateWarning)).Append(";}");
                sb.Append("body{margin:0;background:var(--bg);color:var(--text);font:13px 'Microsoft YaHei UI','Segoe UI',sans-serif;}header{padding:10px 12px;background:var(--bg2);border-bottom:1px solid var(--line);display:flex;gap:8px;align-items:center;position:sticky;top:0;}header span{color:var(--muted);font:12px 'Microsoft YaHei UI';}header input{flex:1;background:var(--bg);border:1px solid var(--line);color:var(--text);padding:6px 8px;border-radius:4px;font:12px 'Microsoft YaHei UI';}table{width:100%;border-collapse:collapse;font:12px Consolas,monospace;}th{text-align:left;color:var(--muted);font-weight:normal;background:var(--bg3);padding:6px;}td{border-bottom:1px solid var(--line);padding:6px;vertical-align:top;}tr:hover{background:var(--bg2);}tr.disputed{background:var(--bg3);}tr.disputed td:first-child{border-left:3px solid var(--warning);}a{color:var(--info);text-decoration:none;cursor:pointer;}.empty{color:var(--weak);font-style:italic;padding:18px 12px;}");
                sb.Append("</style></head><body><header><span>记忆</span><input id='q' placeholder='搜索 key / value'><span id='cnt'></span></header><div id='list'></div>");
                sb.Append("<script>const KEY='ahzscribe.ui.memory';let rows=[];function esc(s){return String(s==null?'':s).replace(/[&<>\"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',\"'\":'&#39;'}[c]));}");
                sb.Append("function render(){let q=(document.getElementById('q').value||'').toLowerCase();let list=document.getElementById('list');let n=0;let h='<table><tr><th>kind</th><th>key</th><th>value</th><th>status</th><th>source</th></tr>';rows.forEach(function(r){if(q&&(r.raw||'').toLowerCase().indexOf(q)<0)return;n++;let cls=r.status==='disputed'?'disputed':'';let a=esc(r.audit||'—');h+='<tr data-audit=\"'+a+'\" class=\"'+cls+'\"><td>'+esc(r.kind)+'</td><td>'+esc(r.key)+'</td><td>'+esc(r.value)+'</td><td>'+esc(r.status)+'</td><td>'+a+'</td></tr>';});h+='</table>';if(n===0)h='<div class=\"empty\">没有匹配的记忆</div>';list.innerHTML=h;document.getElementById('cnt').textContent=n+' 条';}");
                sb.Append("document.getElementById('list').addEventListener('click',function(e){var t=e.target.closest('tr[data-audit]');if(t&&t.dataset.audit){chrome.webview.hostObjects.host.OpenAudit(t.dataset.audit);}});");
                sb.Append("async function load(){try{rows=JSON.parse(await chrome.webview.hostObjects.host.QueryMemories(''));let saved={};try{saved=JSON.parse(localStorage.getItem(KEY)||'{}');}catch(e){}document.getElementById('q').value=saved.q||'';render();}catch(e){document.getElementById('list').innerHTML='<div class=\"empty\">'+esc(e)+'</div>';}}");
                sb.Append("document.getElementById('q').addEventListener('input',function(){render();try{localStorage.setItem(KEY,JSON.stringify({q:this.value}));}catch(e){}});load();");
                sb.Append("</script></body></html>");
                await _web.EnsureCoreWebView2Async(null);
                try { _web.CoreWebView2.AddHostObjectToScript("host", new FaceUiHost(_dataDir, OpenAuditById)); } catch { }
                _web.CoreWebView2.NavigateToString(sb.ToString());
            }
            catch (Exception ex) { try { File.AppendAllText(Path.Combine(_dataDir, "webview.log"), ex + "\n"); } catch { } }
        }
        async void RenderAuditHtml()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("<!doctype html><html><head><meta charset='utf-8'><style>");
                sb.Append(":root{--bg:").Append(Css(Theme.BgPrimary)).Append(";--bg2:").Append(Css(Theme.BgSecondary)).Append(";--bg3:").Append(Css(Theme.BgTertiary)).Append(";--line:").Append(Css(Theme.BorderSubtle)).Append(";--text:").Append(Css(Theme.TextPrimary)).Append(";--muted:").Append(Css(Theme.TextSecondary)).Append(";--weak:").Append(Css(Theme.TextTertiary)).Append(";--info:").Append(Css(Theme.StateInfo)).Append(";--success:").Append(Css(Theme.StateSuccess)).Append(";--danger:").Append(Css(Theme.StateDanger)).Append(";--warning:").Append(Css(Theme.StateWarning)).Append(";}");
                sb.Append("body{margin:0;background:var(--bg);color:var(--text);font:13px 'Microsoft YaHei UI','Segoe UI',sans-serif;}header{padding:10px 12px;background:var(--bg2);border-bottom:1px solid var(--line);display:flex;gap:8px;align-items:center;position:sticky;top:0;z-index:2;}header span{color:var(--muted);font:12px 'Microsoft YaHei UI';}header input{flex:1;background:var(--bg);border:1px solid var(--line);color:var(--text);padding:6px 8px;border-radius:4px;font:12px 'Microsoft YaHei UI';}table{width:100%;border-collapse:collapse;font:12px Consolas,monospace;}th{text-align:left;color:var(--muted);font-weight:normal;background:var(--bg3);padding:6px;}td{border-bottom:1px solid var(--line);padding:5px 6px;vertical-align:top;}tr:hover{background:var(--bg2);}tr.blocked td:first-child{border-left:3px solid var(--danger);} .danger{color:var(--danger);} .ok{color:var(--success);} .warn{color:var(--warning);} .empty{color:var(--weak);font-style:italic;padding:18px 12px;}");
                sb.Append("</style></head><body><header><span>审计</span><input id='q' placeholder='搜索 tool / args / span'><span id='cnt'></span></header><div id='list'></div>");
                sb.Append("<script>const KEY='ahzscribe.ui.audit';let rows=[];function esc(s){return String(s==null?'':s).replace(/[&<>\"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',\"'\":'&#39;'}[c]));}");
                sb.Append("const focus=").Append(System.Text.Json.JsonSerializer.Serialize(_auditFocus ?? "")).Append(";");
                sb.Append("function render(){let q=(document.getElementById('q').value||'').toLowerCase();let list=document.getElementById('list');list.innerHTML='';let n=0;rows.forEach(function(r){if(q&&(r.raw||'').toLowerCase().indexOf(q)<0)return;n++;let cls=r.decision==='blocked'?'row blocked':'';let fb=r.fallback?' <span class=\"warn\">[备用]</span>':'';let d=document.createElement('div');d.className=cls;d.innerHTML='<table><tr><td>'+esc(r.ts)+'</td><td>'+esc(r.span)+'</td><td>'+esc(r.tool)+'</td><td class=\"'+(r.decision==='blocked'?'danger':(r.decision==='allowed'?'ok':'')) +'\">'+esc(r.decision)+fb+'</td><td>'+esc(r.result)+'</td></tr></table>';list.appendChild(d);});if(n===0)list.innerHTML='<div class=\"empty\">没有匹配的审计</div>';document.getElementById('cnt').textContent=n+' 条';}");
                sb.Append("async function load(){try{rows=JSON.parse(await chrome.webview.hostObjects.host.QueryAudit(''));let saved={};try{saved=JSON.parse(localStorage.getItem(KEY)||'{}');}catch(e){}document.getElementById('q').value=saved.q||'';render();if(saved.scroll)setTimeout(()=>document.getElementById('list').scrollTop=saved.scroll,50);}catch(e){document.getElementById('list').innerHTML='<div class=\"empty\">'+esc(e)+'</div>';}}");
                sb.Append("if(focus){let el=document.querySelector('[data-audit=\"'+focus+'\"]');if(el){el.scrollIntoView({block:'center'});el.style.background='var(--bg3)';}}");
                sb.Append("document.getElementById('q').addEventListener('input',function(){render();try{localStorage.setItem(KEY,JSON.stringify({q:this.value,scroll:document.getElementById('list').scrollTop}));}catch(e){}});load();");
                sb.Append("</script></body></html>");
                await _web.EnsureCoreWebView2Async(null);
                try { _web.CoreWebView2.AddHostObjectToScript("host", new FaceUiHost(_dataDir, OpenAuditById)); } catch { }
                _web.CoreWebView2.NavigateToString(sb.ToString());
            }
            catch (Exception ex) { try { File.AppendAllText(Path.Combine(_dataDir, "webview.log"), ex + "\n"); } catch { } }
        }
        public void OpenAuditById(string auditId)
        {
            _auditFocus = auditId;
            Show();
            _tabs.SelectedIndex = 3;
            ShowHtmlTab();
            RenderAuditHtml();
        }
        public void ShowPulse()
        {
            Show();
            _tabs.SelectedIndex = 1;
            RenderPulse();
        }

        static string Css(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        static string PulseHtml(string raw)
        {
            string time = "";
            if (raw.Length >= 19) time = raw.Substring(11, 8);
            string rest = raw.Length > 19 ? raw.Substring(19).Trim() : raw;
            string source = rest, action = "", reason = "";
            int slash = rest.IndexOf(" / ", StringComparison.Ordinal);
            if (slash >= 0)
            {
                source = rest.Substring(0, slash).Trim();
                string[] parts = rest.Substring(slash + 3).Split(new[] { " / " }, StringSplitOptions.None);
                if (parts.Length >= 2) action = parts[1].Trim();
                if (parts.Length >= 3) reason = parts[2].Trim();
            }
            string color = action == "none" ? "var(--weak)" : (action == "memory" ? "var(--success)" : "var(--text)");
            var sb = new StringBuilder();
            sb.Append("<details class='row'><summary><span class='t'>").Append(System.Net.WebUtility.HtmlEncode(time)).Append("</span>");
            sb.Append("<span class='src'>").Append(System.Net.WebUtility.HtmlEncode(source)).Append("</span>");
            sb.Append("<span class='act' style='color:").Append(color).Append("'>").Append(System.Net.WebUtility.HtmlEncode(action.Length == 0 ? rest : action)).Append("</span>");
            if (reason.Length > 0) sb.Append("<span class='why'>").Append(System.Net.WebUtility.HtmlEncode(reason)).Append("</span>");
            sb.Append("<span class='chev'>▸</span></summary>");
            sb.Append("<pre class='detail'>").Append(System.Net.WebUtility.HtmlEncode(raw)).Append("</pre></details>");
            return sb.ToString();
        }

        async void RenderPulse()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("<!doctype html><html><head><meta charset='utf-8'><style>");
                sb.Append(":root{--bg:").Append(Css(Theme.BgPrimary)).Append(";--bg2:").Append(Css(Theme.BgSecondary)).Append(";--bg3:").Append(Css(Theme.BgTertiary)).Append(";--elev:").Append(Css(Theme.BgElevated)).Append(";--line:").Append(Css(Theme.BorderSubtle)).Append(";--text:").Append(Css(Theme.TextPrimary)).Append(";--muted:").Append(Css(Theme.TextSecondary)).Append(";--weak:").Append(Css(Theme.TextTertiary)).Append(";--info:").Append(Css(Theme.StateInfo)).Append(";--success:").Append(Css(Theme.StateSuccess)).Append(";--warning:").Append(Css(Theme.StateWarning)).Append(";--danger:").Append(Css(Theme.StateDanger)).Append(";}");
                sb.Append("*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:13px 'Microsoft YaHei UI','Segoe UI',sans-serif;}");
                sb.Append("header{padding:10px 12px;background:var(--bg2);border-bottom:1px solid var(--line);display:flex;gap:8px;align-items:center;position:sticky;top:0;z-index:2;}header span{color:var(--muted);font:12px 'Microsoft YaHei UI';}header input{flex:1;background:var(--bg);border:1px solid var(--line);color:var(--text);padding:6px 8px;border-radius:4px;font:12px 'Microsoft YaHei UI';}");
                sb.Append(".row{border-bottom:1px solid var(--line);} .row:hover{background:var(--elev);} summary{display:flex;gap:10px;align-items:baseline;padding:7px 12px;cursor:pointer;list-style:none;} summary::-webkit-details-marker{display:none;}.t{font:12px Consolas,monospace;color:var(--muted);min-width:64px;}.src{font:12px Consolas,monospace;color:var(--info);min-width:120px;}.act{font:12px Consolas,monospace;min-width:60px;}.why{font:12px 'Microsoft YaHei UI';color:var(--muted);flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}.chev{color:var(--muted);margin-left:auto;font-size:11px;}details[open] .chev{transform:rotate(90deg);}");
                sb.Append(".detail{margin:0;padding:8px 12px 12px 86px;background:var(--bg3);color:var(--text);font:11.5px Consolas,monospace;white-space:pre-wrap;word-break:break-all;}.lbl{display:block;color:var(--info);font:11px Consolas,monospace;margin-top:6px;}.empty{color:var(--weak);font-style:italic;padding:18px 12px;}.err{color:var(--danger);padding:12px;}");
                sb.Append("</style></head><body><header><span>脉冲</span><input id='q' placeholder='搜索 thought / reason / 工具'><span id='cnt'></span></header><div id='list'><div class='empty'>加载中…</div></div>");
                sb.Append("<script>");
                sb.Append("const KEY='ahzscribe.ui.pulse';let rows=[];function esc(s){return String(s==null?'':s).replace(/[&<>\"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',\"'\":'&#39;'}[c]));}");
                sb.Append("function render(){let q=(document.getElementById('q').value||'').toLowerCase();let list=document.getElementById('list');list.innerHTML='';let n=0;rows.forEach(function(r){if(q&&(r.raw||'').toLowerCase().indexOf(q)<0)return;n++;let color=r.action==='none'?'var(--weak)':(r.action==='memory'?'var(--success)':'var(--text)');let d=document.createElement('details');d.className='row';d.innerHTML='<summary><span class=\"t\">'+esc(r.time)+'</span><span class=\"src\">'+esc(r.triggerType)+'</span><span class=\"act\" style=\"color:'+color+'\">'+esc(r.action)+'</span><span class=\"why\">'+esc(r.reason)+'</span><span class=\"chev\">▸</span></summary><pre class=\"detail\"><span class=\"lbl\">state_restore</span>'+esc(r.stateRestore||r.raw)+'\n<span class=\"lbl\">thought</span>'+esc(r.thought||'（未记录）')+'\n<span class=\"lbl\">action</span>'+esc(r.action)+' / '+esc(r.reason)+'\n<span class=\"lbl\">state_writeback</span>'+esc(r.stateWriteback||'（未记录）')</pre>';list.appendChild(d);});if(n===0)list.innerHTML='<div class=\"empty\">没有匹配的脉冲</div>';document.getElementById('cnt').textContent=n+' 条';}");
                sb.Append("async function load(){try{rows=JSON.parse(await chrome.webview.hostObjects.host.QueryPulses(''));let saved={};try{saved=JSON.parse(localStorage.getItem(KEY)||'{}');}catch(e){}document.getElementById('q').value=saved.q||'';render();if(saved.scroll)setTimeout(()=>document.getElementById('list').scrollTop=saved.scroll,50);}catch(e){document.getElementById('list').innerHTML='<div class=\"err\">'+esc(e)+'</div>';}}");
                sb.Append("document.getElementById('q').addEventListener('input',function(){render();try{localStorage.setItem(KEY,JSON.stringify({q:this.value,scroll:document.getElementById('list').scrollTop}));}catch(e){}});document.getElementById('list').addEventListener('scroll',function(){try{localStorage.setItem(KEY,JSON.stringify({q:document.getElementById('q').value,scroll:this.scrollTop}));}catch(e){}},{passive:true});load();");
                sb.Append("</script></body></html>");
                await _web.EnsureCoreWebView2Async(null);
                try { _web.CoreWebView2.AddHostObjectToScript("host", new FaceUiHost(_dataDir, OpenAuditById)); } catch { }
                _web.CoreWebView2.NavigateToString(sb.ToString());
            }
            catch (Exception ex) { try { File.AppendAllText(Path.Combine(_dataDir, "webview.log"), ex.ToString() + "\n"); } catch { } }
        }
        void EmergencyStop()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AhzScribe", "state");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "EMERGENCY_STOP"), DateTime.Now.ToString("o"), new UTF8Encoding(false));
                AppendChat("Ahz", "已急停，所有脉冲停止", "error");
                if (_topDot != null) _topDot.ForeColor = Theme.DangerButtonBg;
            }
            catch (Exception ex) { AppendChat("Ahz", "急停失败：" + ex.Message, "error"); }
        }
        void UpdateStatus()
        {
            try
            {
                if (InvokeRequired) { BeginInvoke(new Action(UpdateStatus)); return; }
                FaceState st = FaceSession.LoadState(_sessionPath);
                BodyStatus b = _sensor == null ? new BodyStatus() : _sensor.Snapshot();
                _uiBus.Publish(new UiSnapshot { State = st, Body = b });
                int mem = _memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult().Count;
                var sb = new StringBuilder();
                sb.AppendLine("episode     " + (st == null ? "-" : st.CurrentEpisode));
                sb.AppendLine("messages    " + (st == null ? 0 : st.MessageCount));
                sb.AppendLine("focus       " + (st == null ? "" : st.CurrentFocus));
                sb.AppendLine("active_task " + (st == null ? "" : st.ActiveTask));
                sb.AppendLine("intents     " + (st == null || st.OpenIntents == null ? 0 : st.OpenIntents.Count));
                sb.AppendLine("memory      " + mem);
                sb.AppendLine("pulse       " + (st == null || st.LastPulseTrigger == null ? "-" : st.LastPulseTrigger.Type + " / " + st.LastPulseTrigger.ReceivedAt));
                sb.AppendLine();
                sb.AppendLine(_sensor == null ? "" : _sensor.Summary(b));
                _statusView.Text = sb.ToString();
                if (_taskView != null)
                {
                    var ts = new StringBuilder();
                    ts.AppendLine("active_task  " + (st == null ? "" : st.ActiveTask));
                    ts.AppendLine("started_at   " + (st == null ? "" : st.ActiveTaskStartedAt));
                    if (st != null && st.OpenIntents != null)
                        foreach (OpenIntent it in st.OpenIntents) ts.AppendLine(it.Status + "  " + it.CreatedAt + "  " + it.Text);
                    _taskView.Text = ts.ToString();
                }
            }
            catch { }
        }
        void UpdateNav()
        {
            if (_nav == null) return;
            for (int i = 0; i < _nav.Length; i++)
            {
                _nav[i].BackColor = i == _tabs.SelectedIndex ? Theme.BgElevated : Theme.BgBase;
                _nav[i].ForeColor = i == _tabs.SelectedIndex ? Theme.TextPrimary : Theme.TextSecondary;
            }
        }
        void ExtractCurrent()
        {
            try
            {
                List<FaceMessage> msgs = FaceSession.Load(_sessionPath, 5000)
                    .Where(x => x.Kind != "tool").ToList();
                if (msgs.Count > 300) msgs = msgs.Skip(msgs.Count - 300).ToList();
                var sb = new StringBuilder();
                foreach (FaceMessage m in msgs)
                    sb.AppendLine((m.Role ?? "") + ": " + (m.Text ?? ""));
                string source = FaceAudit.Append(_dataDir, "memory", "extract", "allowed", "manual /extract", "manual extraction");
                string debugPath = Path.Combine(_dataDir, "extract-debug.log");
                File.WriteAllText(Path.Combine(_dataDir, "extract-transcript.txt"), sb.ToString(), new UTF8Encoding(false));
                var ex = new MemoryExtractor(_host.Model, s => { try { File.AppendAllText(debugPath, s + Environment.NewLine); } catch { } });
                List<MemoryItem> items = ex.ExtractAsync(sb.ToString(), source, CancellationToken.None).GetAwaiter().GetResult();
                foreach (MemoryItem m in items)
                    _memory.RememberAsync(m, CancellationToken.None).GetAwaiter().GetResult();
                RenderMemory();
                AppendChat("Ahz", "提取完成：" + items.Count + " 条，source=" + source, "chat");
            }
            catch (Exception ex) { AppendChat("Ahz", "提取失败：" + ex.Message, "chat"); }
        }
        string AuditPath()
        {
            string data = !string.IsNullOrEmpty(_settings.BodyData) ? _settings.BodyData : Environment.GetEnvironmentVariable("AHZ_BODY_DATA");
            if (!string.IsNullOrEmpty(data)) return Path.Combine(data, "audit.jsonl");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AhzScribe", "body", "audit.jsonl");
        }

        void HandleAudit(string args)
        {
            var allLines = new List<string>();
            string path = AuditPath();
            if (File.Exists(path)) allLines.AddRange(File.ReadAllLines(path, Encoding.UTF8));
            string facePath = FaceAudit.PathFor(_dataDir);
            if (File.Exists(facePath)) allLines.AddRange(File.ReadAllLines(facePath, Encoding.UTF8));
            if (allLines.Count == 0) { AppendChat("Ahz", "没有审计记录：" + path, "chat"); return; }
            string[] lines = allLines.ToArray();
            string[] parts = (args ?? "").Split(new[] { ' ' }, 2);
            string cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
            if (cmd == "tail")
            {
                int n = 20;
                if (parts.Length > 1) int.TryParse(parts[1], out n);
                if (n < 1) n = 20;
                AppendChat("Ahz", string.Join("\n", lines.Skip(Math.Max(0, lines.Length - n))), "chat");
                return;
            }
            if (cmd == "show")
            {
                string id = parts.Length > 1 ? parts[1].Trim() : "";
                if (id.Length == 0) { AppendChat("Ahz", "用法：/audit show <audit_id>", "chat"); return; }
                string hit = lines.FirstOrDefault(x => x.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit != null && hit.IndexOf("rolled_back_by", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    string link = lines.FirstOrDefault(x => x.IndexOf("audit_link", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        x.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (link != null) hit += "\nrolled_back_by: audit_link present -> " + link;
                }
                AppendChat("Ahz", hit ?? "没有找到 audit_id：" + id, "chat");
                return;
            }
            if (cmd == "export")
            {
                string outPath = Path.Combine(_dataDir, "audit-export.jsonl");
                File.WriteAllLines(outPath, lines, new UTF8Encoding(false));
                AppendChat("Ahz", "已导出 " + lines.Length + " 条到 " + outPath, "chat");
                return;
            }
            AppendChat("Ahz", "用法：/audit tail [n] | /audit show <id> | /audit export", "chat");
        }
        void SaveSettings()
        {
            _settings.BaseUrl = _setBase.Text.Trim();
            _settings.Model = _setModel.Text.Trim();
            _settings.Workspace = _setWorkspace.Text.Trim();
            _settings.BodyPipe = _setPipe.Text.Trim();
            _settings.BodyData = _setBodyData.Text.Trim();
            _settings.ModelNoProxy = _setNoProxy.Checked;
            if (_setNoProxy.Checked) Environment.SetEnvironmentVariable("AHZ_MODEL_NOPROXY", "1");
            else Environment.SetEnvironmentVariable("AHZ_MODEL_NOPROXY", "");
            _settings.Save(_dataDir);
            AppendChat("Ahz", "设置已保存，重启 face 后生效" + (string.IsNullOrEmpty(_settings.Workspace) ? "；工作区为空，propose_upgrade 会被拒绝" : ""), "chat");
        }
        void UpdateTitle(string focus)
        {
            Text = "Ahz Scribe · face v0";
            if (!string.IsNullOrWhiteSpace(focus))
                Text += " · " + Cut(focus, 36);
        }
        async void SendAsync()
        {
            string text = _input.Text.Trim();
            if (text.Length == 0) return;
            _input.Clear();
            _pulseBody.LastUserAt = DateTime.UtcNow;
            FaceState ps = FaceSession.LoadState(_sessionPath);
            if (ps != null) { ps.UnansweredProactive = 0; ps.ProactivePausedUntil = ""; FaceSession.SaveState(_sessionPath, ps); }
            FaceSession.SetFocus(_sessionPath, text);
            UpdateTitle(text);
            AppendChat("你", text, "chat");
            if (text == "/stop")
            {
                string f = StopPath();
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                File.WriteAllText(f, DateTime.Now.ToString("o"));
                _pulseTimer.Change(Timeout.Infinite, Timeout.Infinite);
                AppendChat("Ahz", "EMERGENCY_STOP 已启用。删除 " + f + " 或发 /resume 恢复。", "chat");
                return;
            }
            if (text == "/resume")
            {
                try { File.Delete(StopPath()); } catch { }
                _pulseTimer.Change(60000, 60000);
                AppendChat("Ahz", "已恢复脉冲。", "chat");
                return;
            }
            if (File.Exists(StopPath()))
            {
                AppendChat("Ahz", "EMERGENCY_STOP 生效中。发 /resume 恢复。", "chat");
                return;
            }
            if (text.StartsWith("/intent ", StringComparison.Ordinal))
            {
                string body = text.Substring(8).Trim();
                if (body.Length == 0) { AppendChat("Ahz", "用法：/intent [30m] <要提醒的事>", "chat"); return; }
                string dueAt = "";
                Match mins = Regex.Match(body, @"^(\d+)\s*(分钟|分|m|min)\s*(.*)$", RegexOptions.IgnoreCase);
                if (mins.Success)
                {
                    int delay = Math.Max(1, int.Parse(mins.Groups[1].Value));
                    dueAt = DateTime.Now.AddMinutes(delay).ToString("o");
                    body = mins.Groups[3].Value.Length == 0 ? mins.Value : mins.Groups[3].Value;
                }
                OpenIntent oi = FaceSession.AddIntent(_sessionPath, body, dueAt);
                _memory.RememberAsync(new MemoryItem { Kind = "intent", Key = oi.Id, Value = body, Summary = "intent", DueAt = dueAt }, CancellationToken.None).GetAwaiter().GetResult();
                AppendChat("Ahz", "已记下意图：" + body + (dueAt.Length == 0 ? "" : "（到期 " + dueAt + "）") + "\n下次脉冲会检查（安静时段 23:00–08:00 不提醒）", "chat");
                RenderMemory();
                return;
            }
            if (text == "/pulse")
            {
                FaceSession.SetPulseTrigger(_sessionPath, "timer", "manual /pulse");
                DateTime saved = _pulseBody.LastUserAt;
                _pulseBody.LastUserAt = DateTime.UtcNow.AddSeconds(-701);
                try
                {
                    PulseDecision d = _arbiter.EnqueueAsync(new PulseTrigger { Type = "timer", Payload = "manual /pulse" }, CancellationToken.None).GetAwaiter().GetResult();
                    AppendChat("Ahz", "pulse: " + (d == null ? "null" : d.Action + " / " + (d.Reason ?? "")), "chat");
                }
                finally { _pulseBody.LastUserAt = saved; }
                return;
            }
            if (text == "/proactive-reset")
            {
                FaceState st = FaceSession.LoadState(_sessionPath);
                if (st != null)
                {
                    st.ProactiveCount = 0; st.UnansweredProactive = 0; st.ProactivePausedUntil = "";
                    FaceSession.SaveState(_sessionPath, st);
                }
                AppendChat("Ahz", "主动消息限流已重置", "chat");
                return;
            }
            if (text == "/intent-clear")
            {
                FaceState st = FaceSession.LoadState(_sessionPath);
                if (st != null && st.OpenIntents != null)
                {
                    foreach (OpenIntent it in st.OpenIntents) if (it.Status == "open" || it.Status == "monitoring") it.Status = "cancelled";
                    FaceSession.SaveState(_sessionPath, st);
                }
                AppendChat("Ahz", "已取消当前 open intents", "chat");
                return;
            }
            if (text == "/drive")
            {
                RunDrive();
                return;
            }
            if (text == "/extract")
            {
                ExtractCurrent();
                return;
            }
            if (text.StartsWith("/audit", StringComparison.Ordinal))
            {
                HandleAudit(text.Substring(6).Trim());
                return;
            }
            if (text == "/help")
            {
                AppendChat("Ahz", "命令：\n/ask <问题>  对话\n/tool <任务>  让模型调用工具\n/remember <kind> <key> <value>  写入记忆\n/memory <查询>  检索记忆\n/episode  当前时间线状态\n/intent <事>  记一个提醒意图\n/pulse  立即执行一次脉冲\n/extract  提取最近对话记忆\n/drive  立即跑一次内驱\n/intent-clear  取消所有 open intents\n/proactive-reset  重置主动消息限流\n/audit tail [n]  查看审计\n/audit show <id>  查看一条\n/audit export  导出到 _face-data\n/stop  全局急停\n/resume  恢复\n/help  本帮助", "chat");
                return;
            }
            if (text == "/episode")
            {
                FaceState st = FaceSession.LoadState(_sessionPath);
                AppendChat("Ahz", st == null ? "还没有时间线状态" : "episode " + st.CurrentEpisode + "\nmessage_count " + st.MessageCount + "\nfocus " + (st.CurrentFocus ?? ""), "chat");
                return;
            }
            if (text.StartsWith("/remember ", StringComparison.Ordinal))
            {
                string[] p = text.Split(new[] { ' ' }, 4);
                if (p.Length < 4) { AppendChat("Ahz", "用法：/remember <kind> <key> <value>", "chat"); return; }
                string sa = FaceAudit.Append(_dataDir, "memory", "remember", "allowed", p[2] + "=" + p[3], "user /remember");
                _memory.RememberAsync(new MemoryItem { Kind = p[1], Key = p[2], Value = p[3], Summary = p[2] + "=" + p[3], SourceAuditId = sa }, CancellationToken.None).GetAwaiter().GetResult();
                AppendChat("Ahz", "已记住 " + p[2] + " = " + p[3], "chat");
                RenderMemory();
                return;
            }
            if (text.StartsWith("/memory ", StringComparison.Ordinal))
            {
                List<MemoryItem> hits = _memory.RetrieveAsync(text.Substring(8), 8, CancellationToken.None).GetAwaiter().GetResult();
                AppendChat("Ahz", hits.Count == 0 ? "没有命中记忆" : string.Join("\n", hits.Select(x => "[" + x.Kind + "] " + x.Key + " = " + x.Value)), "chat");
                return;
            }
            bool taskStarted = false;
            _send.Enabled = false;
            try
            {
                FaceSession.BeginTask(_sessionPath, text);
                taskStarted = true;
                _host.Approval = delegate(BrainToolCall call, CancellationToken ct)
                {
                    using (var dlg = new ApprovalForm("工具审批：" + (call.Tool ?? ""), DescribeArgs(call)))
                        return Task.FromResult(dlg.ShowDialog(this) == DialogResult.Yes);
                };
                string answer = text.StartsWith("/tool ", StringComparison.Ordinal)
                    ? await _host.RunToolAsync(text.Substring(6), CancellationToken.None)
                    : await _host.AskAsync(text, CancellationToken.None);
                AppendChat("Ahz", answer ?? "", "chat");
            UpdateStatus();
            }
            catch (Exception ex) { Append("错误：" + ex.Message); }
            finally { if (taskStarted) FaceSession.EndTask(_sessionPath); _send.Enabled = true; _input.Focus(); }
        }

        static string DescribeArgs(BrainToolCall call)
        {
            if (call.Args == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in call.Args) sb.AppendLine(kv.Key + " = " + kv.Value);
            return sb.ToString();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _host.Dispose();
            base.OnFormClosed(e);
        }
    }
}
