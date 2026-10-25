using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AhzScribe.Face
{
    public sealed class FaceMessage
    {
        public string Role { get; set; }
        public string Text { get; set; }
        public string Kind { get; set; } // chat / tool
        public string Ts { get; set; }
        public string Episode { get; set; }
    }

    public sealed class FaceState
    {
        public string CurrentEpisode { get; set; }
        public int MessageCount { get; set; }
        public string LastUserAt { get; set; }
        public string CurrentFocus { get; set; }
        public string LastPulseResult { get; set; }
        public string UpdatedAt { get; set; }
    }

    public static class FaceSession
    {
        const int EpisodeLimit = 40;
        static readonly object Gate = new object();
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public static string StatePath(string sessionPath)
        {
            return Path.Combine(Path.GetDirectoryName(sessionPath), "state.json");
        }

        public static FaceState LoadState(string sessionPath)
        {
            try
            {
                string f = StatePath(sessionPath);
                if (!File.Exists(f)) return null;
                return JsonSerializer.Deserialize<FaceState>(File.ReadAllText(f, Encoding.UTF8), Json);
            }
            catch { return null; }
        }

        public static void SetFocus(string sessionPath, string text)
        {
            try
            {
                lock (Gate)
                {
                    FaceState st = LoadState(sessionPath) ?? new FaceState { CurrentEpisode = NewEpisodeId() };
                    st.CurrentFocus = string.IsNullOrEmpty(text) ? "" : (text.Length > 200 ? text.Substring(0, 200) : text);
                    SaveState(sessionPath, st);
                }
            }
            catch { }
        }
        static void SaveState(string sessionPath, FaceState st)
        {
            try
            {
                st.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath(sessionPath)));
                File.WriteAllText(StatePath(sessionPath), JsonSerializer.Serialize(st, Json), new UTF8Encoding(false));
            }
            catch { }
        }

        public static List<FaceMessage> Load(string path, int max)
        {
            var list = new List<FaceMessage>();
            try
            {
                if (!File.Exists(path)) return list;
                foreach (string line in File.ReadLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        FaceMessage m = JsonSerializer.Deserialize<FaceMessage>(line, Json);
                        if (m != null) list.Add(m);
                    }
                    catch { }
                }
            }
            catch { }
            if (list.Count > max) list.RemoveRange(0, list.Count - max);
            return list;
        }

        public static void Append(string path, FaceMessage message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    FaceState st = LoadState(path);
                    if (st == null || string.IsNullOrEmpty(st.CurrentEpisode))
                    {
                        st = new FaceState { CurrentEpisode = NewEpisodeId(), MessageCount = 0 };
                    }
                    if (st.MessageCount >= EpisodeLimit)
                    {
                        st.CurrentEpisode = NewEpisodeId();
                        st.MessageCount = 0;
                    }
                    message.Episode = st.CurrentEpisode;
                    message.Ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    File.AppendAllText(path, JsonSerializer.Serialize(message, Json) + Environment.NewLine, new UTF8Encoding(false));
                    st.MessageCount++;
                    if (message.Role == "你") st.LastUserAt = message.Ts;
                    SaveState(path, st);
                }
            }
            catch { }
        }

        static string NewEpisodeId()
        {
            return "ep-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
