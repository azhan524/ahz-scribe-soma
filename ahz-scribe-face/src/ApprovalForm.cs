using System;
using System.Drawing;
using System.Windows.Forms;

namespace AhzScribe.Face
{
    public sealed class ApprovalForm : Form
    {
        public bool Approved { get; private set; }

        public ApprovalForm(string title, string change)
        {
            Text = (title ?? "").StartsWith("工具审批", StringComparison.Ordinal) ? "工具审批" : "批准自我改造？";
            Width = 620;
            Height = 360;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgPrimary;
            ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var head = new Label();
            head.Text = string.IsNullOrEmpty(title) ? "自我改造申请" : title;
            head.Dock = DockStyle.Top;
            head.Height = 44;
            head.Padding = new Padding(18, 12, 18, 0);
            head.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            head.ForeColor = Theme.TextPrimary;

            var body = new TextBox();
            body.Multiline = true;
            body.ReadOnly = true;
            body.ScrollBars = ScrollBars.Vertical;
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.BgPrimary;
            body.ForeColor = Theme.TextPrimary;
            body.BorderStyle = BorderStyle.None;
            body.Font = new Font("Consolas", 9.5f);
            body.Text = change ?? "";
            body.Padding = new Padding(18, 10, 18, 10);

            var buttons = new Panel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 58;
            buttons.BackColor = Theme.BgPrimary;

            var no = new Button();
            no.Text = "驳回";
            no.Width = 96;
            no.Dock = DockStyle.Right;
            no.FlatStyle = FlatStyle.Flat;
            no.FlatAppearance.BorderColor = Theme.BorderStrong;
            no.BackColor = Theme.BgSecondary;
            no.ForeColor = Theme.TextPrimary;
            no.Click += delegate { Approved = false; DialogResult = DialogResult.No; Close(); };

            var yes = new Button();
            yes.Text = "批准";
            yes.Width = 96;
            yes.Dock = DockStyle.Right;
            yes.FlatStyle = FlatStyle.Flat;
            yes.FlatAppearance.BorderSize = 0;
            yes.BackColor = Theme.StateInfo;
            yes.ForeColor = Theme.TextPrimary;
            yes.Click += delegate { Approved = true; DialogResult = DialogResult.Yes; Close(); };

            buttons.Controls.Add(no);
            buttons.Controls.Add(yes);

            Controls.Add(body);
            Controls.Add(buttons);
            Controls.Add(head);
            AcceptButton = yes;
            CancelButton = no;
        }
    }
}
