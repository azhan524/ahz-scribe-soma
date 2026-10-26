using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AhzScribe.Brain;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    public sealed class FaceForm : Form
    {
        readonly RichTextBox _output;
        readonly TextBox _trace;
        readonly TextBox _traceFilter;
        readonly StringBuilder _traceRaw;
        readonly TextBox _memoryView;
        readonly TextBox _memoryFilter;
        readonly TextBox _setBase, _setModel, _setWorkspace, _setPipe;
        readonly TabControl _tabs;
        readonly Label _status;
        readonly Panel _side;
        readonly Button[] _nav;
        readonly TextBox _input;
        readonly Button _send;
        readonly BrainHost _host;
        readonly FacePulseBody _pulseBody;
        readonly PulseScheduler _pulse;
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
            BackColor = Color.FromArgb(24, 26, 30);
            ForeColor = Color.FromArgb(220, 224, 230);

            _dataDir = Path.Combine(AppContext.BaseDirectory, "_face-data");
            _sessionPath = Path.Combine(_dataDir, "sessions.jsonl");
            Directory.CreateDirectory(_dataDir);
            _settings = FaceSettings.Load(_dataDir);
            if (!string.IsNullOrEmpty(_settings.BodyPipe)) Environment.SetEnvironmentVariable("AHZ_BODY_PIPE", _settings.BodyPipe);
            _memory = new MemoryStore(Path.Combine(_dataDir, "memory.jsonl"));
            _host = new BrainHost(_memory, CreateModel(out _modelName));
            _host.Trace = delegate(string line) { AppendTrace(line); };
            _pulseBody = new FacePulseBody();
            _pulse = new PulseScheduler(_pulseBody, _memory, new FacePulseAudit(AppendTrace),
                new FacePulseSink(OnPulse), new IntentPulseDecider(_memory));
            _pulse.Start(60000);
            FormClosed += delegate { _pulse.Dispose(); };
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
            _output.BackColor = Color.FromArgb(30, 33, 38);
            _output.ForeColor = Color.FromArgb(220, 224, 230);
            _output.BorderStyle = BorderStyle.None;
            _output.Font = new Font("Consolas", 10f);

            _trace = new TextBox();
            _trace.Multiline = true;
            _trace.ReadOnly = true;
            _trace.ScrollBars = ScrollBars.Vertical;
            _trace.Dock = DockStyle.Fill;
            _trace.BackColor = Color.FromArgb(26, 28, 32);
            _trace.ForeColor = Color.FromArgb(180, 190, 205);
            _trace.BorderStyle = BorderStyle.None;
            _trace.Font = new Font("Consolas", 9f);

            _traceRaw = new StringBuilder();
            _traceFilter = new TextBox();
            _traceFilter.Dock = DockStyle.Top;
            _traceFilter.BackColor = Color.FromArgb(38, 41, 48);
            _traceFilter.ForeColor = Color.White;
            _traceFilter.BorderStyle = BorderStyle.FixedSingle;
            _traceFilter.PlaceholderText = "过滤轨迹...";
            _traceFilter.TextChanged += delegate { RefreshTrace(); };

            _memoryView = new TextBox();
            _memoryView.Multiline = true;
            _memoryView.ReadOnly = true;
            _memoryView.ScrollBars = ScrollBars.Vertical;
            _memoryView.Dock = DockStyle.Fill;
            _memoryView.BackColor = Color.FromArgb(26, 28, 32);
            _memoryView.ForeColor = Color.FromArgb(180, 190, 205);
            _memoryView.BorderStyle = BorderStyle.None;
            _memoryView.Font = new Font("Consolas", 9f);

            _memoryFilter = new TextBox();
            _memoryFilter.Dock = DockStyle.Top;
            _memoryFilter.BackColor = Color.FromArgb(38, 41, 48);
            _memoryFilter.ForeColor = Color.White;
            _memoryFilter.BorderStyle = BorderStyle.FixedSingle;
            _memoryFilter.PlaceholderText = "搜索记忆...";
            _memoryFilter.TextChanged += delegate { RenderMemory(); };

            _setBase = MakeSettingBox(_settings.BaseUrl);
            _setModel = MakeSettingBox(_settings.Model);
            _setWorkspace = MakeSettingBox(_settings.Workspace);
            _setPipe = MakeSettingBox(_settings.BodyPipe);
            _setWorkspace.PlaceholderText = "工作区路径；留空则不能自我迭代";

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Microsoft YaHei UI", 9f);
            _tabs.Appearance = TabAppearance.FlatButtons;
            _tabs.ItemSize = new Size(0, 1);
            _tabs.SizeMode = TabSizeMode.Fixed;
            var chatPage = new TabPage("对话");
            chatPage.BackColor = Color.FromArgb(24, 26, 30);
            chatPage.Controls.Add(_output);
            var tracePage = new TabPage("轨迹");
            tracePage.BackColor = Color.FromArgb(24, 26, 30);
            var tracePanel = new Panel();
            tracePanel.Dock = DockStyle.Fill;
            tracePanel.Controls.Add(_trace);
            tracePanel.Controls.Add(_traceFilter);
            tracePage.Controls.Add(tracePanel);
            var memoryPage = new TabPage("记忆");
            memoryPage.BackColor = Color.FromArgb(24, 26, 30);
            var memoryPanel = new Panel();
            memoryPanel.Dock = DockStyle.Fill;
            memoryPanel.Controls.Add(_memoryView);
            memoryPanel.Controls.Add(_memoryFilter);
            memoryPage.Controls.Add(memoryPanel);
            var settingsPage = new TabPage("设置");
            settingsPage.BackColor = Color.FromArgb(24, 26, 30);
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(18);
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowCount = 5;
            for (int i = 0; i < 4; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            table.Controls.Add(new Label { Text = "模型 Base URL", ForeColor = Color.FromArgb(200, 210, 220), AutoSize = true }, 0, 0);
            table.Controls.Add(_setBase, 1, 0);
            table.Controls.Add(new Label { Text = "模型名", ForeColor = Color.FromArgb(200, 210, 220), AutoSize = true }, 0, 1);
            table.Controls.Add(_setModel, 1, 1);
            table.Controls.Add(new Label { Text = "工作区", ForeColor = Color.FromArgb(200, 210, 220), AutoSize = true }, 0, 2);
            table.Controls.Add(_setWorkspace, 1, 2);
            table.Controls.Add(new Label { Text = "body 管道", ForeColor = Color.FromArgb(200, 210, 220), AutoSize = true }, 0, 3);
            table.Controls.Add(_setPipe, 1, 3);
            var save = new Button();
            save.Text = "保存（重启 face 生效）";
            save.Width = 220;
            save.FlatStyle = FlatStyle.Flat;
            save.FlatAppearance.BorderSize = 0;
            save.BackColor = Color.FromArgb(64, 120, 220);
            save.ForeColor = Color.White;
            save.Click += delegate { SaveSettings(); };
            table.Controls.Add(save, 1, 4);
            settingsPage.Controls.Add(table);
            _tabs.TabPages.Add(chatPage);
            _tabs.TabPages.Add(tracePage);
            _tabs.TabPages.Add(memoryPage);
            _tabs.TabPages.Add(settingsPage);
            _side = new Panel();
            _side.Dock = DockStyle.Left;
            _side.Width = 168;
            _side.BackColor = Color.FromArgb(18, 20, 24);
            var brand = new Label();
            brand.Text = "AHZ\nSCRIBE";
            brand.Dock = DockStyle.Top;
            brand.Height = 72;
            brand.Padding = new Padding(16, 14, 0, 0);
            brand.Font = new Font("Consolas", 13f, FontStyle.Bold);
            brand.ForeColor = Color.FromArgb(100, 160, 255);
            _side.Controls.Add(brand);
            string[] navNames = { "对话", "轨迹", "记忆", "设置" };
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
                b.BackColor = Color.FromArgb(18, 20, 24);
                b.ForeColor = Color.FromArgb(180, 188, 200);
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
            search.BackColor = Color.FromArgb(18, 20, 24);
            search.ForeColor = Color.FromArgb(180, 188, 200);
            search.TextAlign = ContentAlignment.MiddleLeft;
            search.Padding = new Padding(18, 0, 0, 0);
            search.Click += delegate
            {
                using (var f = new HistorySearchForm(_sessionPath))
                    if (f.ShowDialog(this) == DialogResult.OK && f.SelectedMessage != null) JumpTo(f.SelectedMessage);
            };
            _side.Controls.Add(search);
            _side.Controls.Add(brand);
            _tabs.SelectedIndexChanged += delegate { UpdateNav(); };
            UpdateNav();
            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 120;
            bottom.BackColor = Color.FromArgb(24, 26, 30);

            _input = new TextBox();
            _input.Dock = DockStyle.Fill;
            _input.Multiline = true;
            _input.PlaceholderText = "输入问题；/tool 前缀让模型使用工具";
            _input.ScrollBars = ScrollBars.Vertical;
            _input.Font = new Font("Microsoft YaHei UI", 10f);
            _input.BackColor = Color.FromArgb(38, 41, 48);
            _input.ForeColor = Color.White;
            _input.BorderStyle = BorderStyle.FixedSingle;

            _send = new Button();
            _send.Text = "发送";
            _send.Dock = DockStyle.Right;
            _send.Width = 88;
            _send.FlatStyle = FlatStyle.Flat;
            _send.FlatAppearance.BorderSize = 0;
            _send.BackColor = Color.FromArgb(64, 120, 220);
            _send.ForeColor = Color.White;
            _send.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            _send.Click += delegate { SendAsync(); };
            _input.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; SendAsync(); }
            };

            _status = new Label();
            _status.Dock = DockStyle.Top;
            _status.Height = 22;
            _status.BackColor = Color.FromArgb(20, 22, 26);
            _status.ForeColor = Color.FromArgb(160, 170, 185);
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


            var main = new Panel();
            main.Dock = DockStyle.Fill;
            main.Controls.Add(_tabs);
            main.Controls.Add(_side);
            Controls.Add(main);
            Controls.Add(bottom);

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
            return new OpenAiToolModel(new ModelOptions { BaseUrl = baseUrl, Model = model, ApiKey = key });
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
                        _memory.RememberAsync(new MemoryItem
                        {
                            Kind = "episodic",
                            Key = "episode:" + g.Key,
                            Value = summary,
                            Summary = "episode " + g.Key,
                            Status = "active"
                        }, CancellationToken.None).GetAwaiter().GetResult();
                    }
                }).ConfigureAwait(false);
            }
            catch { }
        }

        static string Cut(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
        void AppendHistory(FaceMessage m)
        {
            string role = m.Role ?? "";
            _output.SelectionColor = role == "你" ? Color.FromArgb(110, 170, 255) : Color.FromArgb(140, 220, 170);
            _output.AppendText(role + "：");
            _output.SelectionColor = Color.FromArgb(220, 224, 230);
            _output.AppendText((m.Text ?? "") + Environment.NewLine + Environment.NewLine);
        }
        void AppendChat(string role, string text, string kind)
        {
            FaceSession.Append(_sessionPath, new FaceMessage { Role = role, Text = text, Kind = kind });
            _output.SelectionColor = role == "你" ? Color.FromArgb(110, 170, 255) : Color.FromArgb(140, 220, 170);
            _output.AppendText(role + "：");
            _output.SelectionColor = Color.FromArgb(220, 224, 230);
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
            b.BackColor = Color.FromArgb(38, 41, 48);
            b.ForeColor = Color.White;
            b.BorderStyle = BorderStyle.FixedSingle;
            b.Font = new Font("Consolas", 9.5f);
            b.Margin = new Padding(0, 4, 0, 4);
            return b;
        }
        void UpdateStatus()
        {
            try
            {
                FaceState st = FaceSession.LoadState(_sessionPath);
                int mem = _memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult().Count;
                string ep = st == null || string.IsNullOrEmpty(st.CurrentEpisode) ? "-" : st.CurrentEpisode;
                _status.Text = "episode " + ep + "   memory " + mem + "   body " + (Environment.GetEnvironmentVariable("AHZ_BODY_PIPE") ?? "ahz-scribe-body");
            }
            catch { }
        }
        void OnPulse(PulseEvent evt)
        {
            if (InvokeRequired) { BeginInvoke(new Action<PulseEvent>(OnPulse), evt); return; }
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
        void UpdateNav()
        {
            if (_nav == null) return;
            for (int i = 0; i < _nav.Length; i++)
            {
                _nav[i].BackColor = i == _tabs.SelectedIndex ? Color.FromArgb(30, 46, 72) : Color.FromArgb(18, 20, 24);
                _nav[i].ForeColor = i == _tabs.SelectedIndex ? Color.White : Color.FromArgb(180, 188, 200);
            }
        }
        void SaveSettings()
        {
            _settings.BaseUrl = _setBase.Text.Trim();
            _settings.Model = _setModel.Text.Trim();
            _settings.Workspace = _setWorkspace.Text.Trim();
            _settings.BodyPipe = _setPipe.Text.Trim();
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
            FaceSession.SetFocus(_sessionPath, text);
            UpdateTitle(text);
            AppendChat("你", text, "chat");
            if (text.StartsWith("/intent ", StringComparison.Ordinal))
            {
                string body = text.Substring(8).Trim();
                if (body.Length == 0) { AppendChat("Ahz", "用法：/intent <要提醒的事>", "chat"); return; }
                _memory.RememberAsync(new MemoryItem { Kind = "intent", Key = "intent:" + Guid.NewGuid().ToString("N"), Value = body, Summary = "intent" }, CancellationToken.None).GetAwaiter().GetResult();
                AppendChat("Ahz", "已记下意图：" + body + "\n下次脉冲会提醒（安静时段 23:00–08:00 不提醒）", "chat");
                RenderMemory();
                return;
            }
            if (text == "/pulse")
            {
                PulseDecision d = _pulse.TickAsync(CancellationToken.None).GetAwaiter().GetResult();
                AppendChat("Ahz", "pulse: " + (d == null ? "null" : d.Action + " / " + (d.Reason ?? "")), "chat");
                return;
            }
            if (text == "/help")
            {
                AppendChat("Ahz", "命令：\n/ask <问题>  对话\n/tool <任务>  让模型调用工具\n/remember <kind> <key> <value>  写入记忆\n/memory <查询>  检索记忆\n/episode  当前时间线状态\n/intent <事>  记一个提醒意图\n/pulse  立即执行一次脉冲\n/help  本帮助", "chat");
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
                _memory.RememberAsync(new MemoryItem { Kind = p[1], Key = p[2], Value = p[3], Summary = p[2] + "=" + p[3] }, CancellationToken.None).GetAwaiter().GetResult();
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
            _send.Enabled = false;
            try
            {
                _host.Approval = delegate(BrainToolCall call, CancellationToken ct)
                {
                    DialogResult r = MessageBox.Show(this,
                        "批准执行 " + call.Tool + " ？\n\n" + DescribeArgs(call),
                        "工具审批", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                    return Task.FromResult(r == DialogResult.Yes);
                };
                string answer = text.StartsWith("/tool ", StringComparison.Ordinal)
                    ? await _host.RunToolAsync(text.Substring(6), CancellationToken.None)
                    : await _host.AskAsync(text, CancellationToken.None);
                AppendChat("Ahz", answer ?? "", "chat");
            UpdateStatus();
            }
            catch (Exception ex) { Append("错误：" + ex.Message); }
            finally { _send.Enabled = true; _input.Focus(); }
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
