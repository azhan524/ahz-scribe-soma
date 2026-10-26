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
        readonly TextBox _memoryView;
        readonly TextBox _setBase, _setModel, _setWorkspace, _setPipe;
        readonly TabControl _tabs;
        readonly TextBox _input;
        readonly Button _send;
        readonly BrainHost _host;
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
            _host.Policy = new SelfIterationPolicy();
            _host.Propose = delegate(BrainToolCall call, CancellationToken ct)
            {
                string title = call.Args != null && call.Args.ContainsKey("title") ? Convert.ToString(call.Args["title"]) : "自我改造申请";
                string change = call.Args != null && call.Args.ContainsKey("change") ? Convert.ToString(call.Args["change"]) : "";
                DialogResult r = MessageBox.Show(this, title + "\\n\\n" + change, "批准自我改造？", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r == DialogResult.Yes)
                {
                    string root = string.IsNullOrEmpty(_settings.Workspace) ? (Environment.GetEnvironmentVariable("AHZ_WORKSPACE") ?? "") : _settings.Workspace;
                    _host.Policy.Begin(root);
                }
                return Task.FromResult(r == DialogResult.Yes);
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

            _memoryView = new TextBox();
            _memoryView.Multiline = true;
            _memoryView.ReadOnly = true;
            _memoryView.ScrollBars = ScrollBars.Vertical;
            _memoryView.Dock = DockStyle.Fill;
            _memoryView.BackColor = Color.FromArgb(26, 28, 32);
            _memoryView.ForeColor = Color.FromArgb(180, 190, 205);
            _memoryView.BorderStyle = BorderStyle.None;
            _memoryView.Font = new Font("Consolas", 9f);

            _setBase = new TextBox(); _setModel = new TextBox(); _setWorkspace = new TextBox(); _setPipe = new TextBox();
            _setBase.Text = _settings.BaseUrl; _setModel.Text = _settings.Model; _setWorkspace.Text = _settings.Workspace; _setPipe.Text = _settings.BodyPipe;

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Microsoft YaHei UI", 9f);
            var chatPage = new TabPage("对话");
            chatPage.BackColor = Color.FromArgb(24, 26, 30);
            chatPage.Controls.Add(_output);
            var tracePage = new TabPage("轨迹");
            tracePage.BackColor = Color.FromArgb(24, 26, 30);
            tracePage.Controls.Add(_trace);
            var memoryPage = new TabPage("记忆");
            memoryPage.BackColor = Color.FromArgb(24, 26, 30);
            memoryPage.Controls.Add(_memoryView);
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
            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 56;
            bottom.BackColor = Color.FromArgb(24, 26, 30);

            _input = new TextBox();
            _input.Dock = DockStyle.Fill;
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

            var pad = new Panel();
            pad.Dock = DockStyle.Fill;
            pad.Padding = new Padding(10, 8, 8, 8);
            pad.Controls.Add(_input);
            bottom.Controls.Add(pad);
            bottom.Controls.Add(_send);

            Controls.Add(_tabs);
            Controls.Add(bottom);

            Append("face v0 · model=" + _modelName);
            Append("输入问题；/tool 前缀让模型使用工具。危险操作会弹确认框。");
            List<FaceMessage> history = FaceSession.Load(_sessionPath, 200);
            foreach (FaceMessage old in history)
            {
                if (old.Kind == "tool") AppendTrace(old.Text);
                else Append((old.Role ?? "") + "：" + (old.Text ?? ""));
            }
            RememberOldEpisodes(history);
            RenderMemory();
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
        void AppendChat(string role, string text, string kind)
        {
            FaceSession.Append(_sessionPath, new FaceMessage { Role = role, Text = text, Kind = kind });
            Append(role + "：" + text);
        }
        void AppendTrace(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendTrace), text); return; }
            FaceSession.Append(_sessionPath, new FaceMessage { Role = "工具", Text = text, Kind = "tool" });
            _trace.AppendText("[工具] " + text + Environment.NewLine);
            _trace.SelectionStart = _trace.TextLength;
            _trace.ScrollToCaret();
        }
        void RenderMemory()
        {
            try
            {
                List<MemoryItem> all = _memory.LoadAllAsync(CancellationToken.None).GetAwaiter().GetResult();
                var sb = new System.Text.StringBuilder();
                foreach (MemoryItem m in all.Skip(Math.Max(0, all.Count - 30)))
                    sb.AppendLine("[" + (m.Kind ?? "") + "] " + (m.Key ?? "") + " = " + Cut(m.Value, 200));
                _memoryView.Text = sb.ToString();
            }
            catch { }
        }
        void SaveSettings()
        {
            _settings.BaseUrl = _setBase.Text.Trim();
            _settings.Model = _setModel.Text.Trim();
            _settings.Workspace = _setWorkspace.Text.Trim();
            _settings.BodyPipe = _setPipe.Text.Trim();
            _settings.Save(_dataDir);
            AppendChat("Ahz", "设置已保存，重启 face 后生效", "chat");
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
            FaceSession.SetFocus(_sessionPath, text);
            UpdateTitle(text);
            AppendChat("你", text, "chat");
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
