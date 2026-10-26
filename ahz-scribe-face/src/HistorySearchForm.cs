using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AhzScribe.Face
{
    public sealed class HistorySearchForm : Form
    {
        readonly List<FaceMessage> _all;
        readonly TextBox _query;
        readonly ListBox _results;
        readonly Label _count;

        public HistorySearchForm(string sessionPath)
        {
            Text = "搜索对话";
            Width = 720;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 26, 30);
            ForeColor = Color.FromArgb(220, 226, 234);
            Font = new Font("Microsoft YaHei UI", 9f);

            _all = FaceSession.Load(sessionPath, 5000);

            _query = new TextBox();
            _query.Dock = DockStyle.Top;
            _query.BackColor = Color.FromArgb(38, 41, 48);
            _query.ForeColor = Color.White;
            _query.BorderStyle = BorderStyle.FixedSingle;
            _query.PlaceholderText = "搜索对话内容...";
            _query.TextChanged += delegate { Search(); };

            _results = new ListBox();
            _results.Dock = DockStyle.Fill;
            _results.BackColor = Color.FromArgb(28, 31, 36);
            _results.ForeColor = Color.FromArgb(210, 218, 228);
            _results.BorderStyle = BorderStyle.None;
            _results.Font = new Font("Consolas", 9f);

            _count = new Label();
            _count.Dock = DockStyle.Bottom;
            _count.Height = 26;
            _count.Padding = new Padding(6, 4, 0, 0);
            _count.ForeColor = Color.FromArgb(160, 170, 185);

            Controls.Add(_results);
            Controls.Add(_count);
            Controls.Add(_query);
            ActiveControl = _query;
            Search();
        }

        void Search()
        {
            string q = _query.Text.Trim();
            _results.BeginUpdate();
            _results.Items.Clear();
            int n = 0;
            foreach (FaceMessage m in _all)
            {
                string text = m.Text ?? "";
                if (q.Length > 0 && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && (m.Role ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string line = (m.Role ?? "") + "：" + text.Replace("\r", " ").Replace("\n", " ");
                if (line.Length > 260) line = line.Substring(0, 260) + "…";
                _results.Items.Add(line);
                n++;
                if (n >= 500) break;
            }
            _results.EndUpdate();
            _count.Text = "命中 " + n + " 条 / 共 " + _all.Count + " 条";
        }
    }
}
