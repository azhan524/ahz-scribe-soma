using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    /// <summary>
    /// Local body sensor. No LLM, no tools; it only samples the machine.
    /// </summary>
    public sealed class BodySensor
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public BodyStatus Snapshot()
        {
            var s = new BodyStatus();
            try
            {
                var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (GetLastInputInfo(ref info))
                    s.IdleSeconds = Math.Max(0, Environment.TickCount - (int)info.dwTime) / 1000;
            }
            catch { }
            try
            {
                IntPtr h = GetForegroundWindow();
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, sb.Capacity);
                s.ForegroundTitle = sb.ToString();
                s.ForegroundApp = s.ForegroundTitle;
            }
            catch { }
            try { s.NetworkOnline = NetworkInterface.GetIsNetworkAvailable(); } catch { }
            try
            {
                s.Locked = Process.GetProcessesByName("LogonUI").Length > 0;
            }
            catch { }
            return s;
        }

        public string Summary(BodyStatus s)
        {
            if (s == null) return "身体状态：未知";
            string fg = string.IsNullOrEmpty(s.ForegroundTitle) ? "无前台窗口" : s.ForegroundTitle;
            if (fg.Length > 40) fg = fg.Substring(0, 40) + "…";
            string battery = "";
            try
            {
                PowerStatus p = SystemInformation.PowerStatus;
                battery = " | 电池" + (int)(p.BatteryLifePercent * 100) + "%";
            }
            catch { }
            string disk = "";
            try
            {
                DriveInfo d = new DriveInfo(Path.GetPathRoot(Application.StartupPath));
                disk = " | 磁盘剩余" + (d.AvailableFreeSpace / 1024 / 1024 / 1024) + "GB";
            }
            catch { }
            return "身体状态：空闲" + (s.IdleSeconds / 60) + "分钟 | 前台 " + fg +
                " | 网络" + (s.NetworkOnline ? "在线" : "离线") + battery + disk;
        }
    }
}
