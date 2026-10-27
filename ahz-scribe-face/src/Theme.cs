using System.Drawing;

namespace AhzScribe.Face
{
    // 颜色唯一来源：硅基生物 UI 配色规划书 v1.1。
    // 组件只允许引用这里的 token，不允许写 Color.FromArgb。
    internal static class Theme
    {
        public static readonly Color BgBase = Color.FromArgb(0x12, 0x12, 0x12);
        public static readonly Color BgPrimary = Color.FromArgb(0x1E, 0x1E, 0x1E);
        public static readonly Color BgSecondary = Color.FromArgb(0x25, 0x25, 0x26);
        public static readonly Color BgTertiary = Color.FromArgb(0x2D, 0x2D, 0x30);
        public static readonly Color BgElevated = Color.FromArgb(0x3E, 0x3E, 0x42);
        public static readonly Color BgDisputed = Color.FromArgb(0x3A, 0x35, 0x20);
        public static readonly Color BorderSubtle = Color.FromArgb(0x33, 0x33, 0x33);
        public static readonly Color BorderStrong = Color.FromArgb(0x4A, 0x4A, 0x4F);

        public static readonly Color TextPrimary = Color.FromArgb(0xCC, 0xCC, 0xCC);
        public static readonly Color TextSecondary = Color.FromArgb(0x9D, 0x9D, 0x9D);
        public static readonly Color TextTertiary = Color.FromArgb(0x80, 0x80, 0x80);
        public static readonly Color TextDisabled = Color.FromArgb(0x5A, 0x5A, 0x5A);

        public static readonly Color StateInfo = Color.FromArgb(0x37, 0x94, 0xFF);
        public static readonly Color StateInfoDim = Color.FromArgb(0x1F, 0x5A, 0x99);
        public static readonly Color StateSuccess = Color.FromArgb(0x4E, 0xC9, 0xB0);
        public static readonly Color StateWarning = Color.FromArgb(0xD7, 0xBA, 0x7D);
        public static readonly Color StateDanger = Color.FromArgb(0xF1, 0x4C, 0x4C);
        public static readonly Color StateDrive = Color.FromArgb(0xC5, 0x86, 0xC0);
        public static readonly Color StateNeutral = Color.FromArgb(0x80, 0x80, 0x80);

        public static readonly Color DangerButtonFg = Color.White;
    }
}
