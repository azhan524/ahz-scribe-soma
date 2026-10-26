using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AhzScribe.Face
{
    public sealed class FaceSettings
    {
        public string BaseUrl { get; set; } = "https://api.deepseek.com";
        public string Model { get; set; } = "deepseek-v4-pro";
        public string Workspace { get; set; } = "";
        public string BodyPipe { get; set; } = "ahz-scribe-body";
        public string BodyData { get; set; } = "";
        public bool ModelNoProxy { get; set; }
        public int ProactiveDailyMax { get; set; } = 2;
        public int QuietStartHour { get; set; } = 23;
        public int QuietEndHour { get; set; } = 8;
        public int UnansweredPauseHours { get; set; } = 24;

        public static FaceSettings Load(string dataDir)
        {
            try
            {
                string f = Path.Combine(dataDir, "settings.json");
                if (!File.Exists(f)) return FromEnv();
                FaceSettings s = JsonSerializer.Deserialize<FaceSettings>(File.ReadAllText(f, Encoding.UTF8));
                return s ?? FromEnv();
            }
            catch { return FromEnv(); }
        }

        public static FaceSettings FromEnv()
        {
            return new FaceSettings
            {
                BaseUrl = Environment.GetEnvironmentVariable("AHZ_MODEL_BASE") ?? "https://api.deepseek.com",
                Model = Environment.GetEnvironmentVariable("AHZ_MODEL_NAME") ?? "deepseek-v4-pro",
                Workspace = Environment.GetEnvironmentVariable("AHZ_WORKSPACE") ?? "",
                BodyPipe = Environment.GetEnvironmentVariable("AHZ_BODY_PIPE") ?? "ahz-scribe-body",
                BodyData = Environment.GetEnvironmentVariable("AHZ_BODY_DATA") ?? ""
            };
        }

        public void Save(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "settings.json"), JsonSerializer.Serialize(this), new UTF8Encoding(false));
        }
    }
}
