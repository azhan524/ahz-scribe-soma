using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AhzScribe.Face
{
    public static class FaceAudit
    {
        static readonly object Gate = new object();

        public static string PathFor(string dataDir)
        {
            return Path.Combine(dataDir, "audit.jsonl");
        }

        public static string Append(string dataDir, string span, string tool, string decision, string result, string reason)
        {
            lock (Gate)
            {
                try
                {
                    string id = "face_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                    Directory.CreateDirectory(dataDir);
                    var rec = new
                    {
                        audit_id = id,
                        ts = DateTimeOffset.UtcNow.ToString("o"),
                        span = span,
                        tool = tool,
                        decision = decision,
                        result = result,
                        reason = reason,
                        source = "face"
                    };
                    File.AppendAllText(PathFor(dataDir), JsonSerializer.Serialize(rec) + Environment.NewLine, new UTF8Encoding(false));
                    return id;
                }
                catch { return ""; }
            }
        }
    }
}
