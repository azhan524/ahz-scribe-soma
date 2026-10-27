using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AhzScribe.Face
{
    public static class UiContract
    {
        public const string ExpectedVersion = "1.2";

        public static string Validate(string path)
        {
            if (!File.Exists(path)) return "contract_missing:" + path;
            string text = File.ReadAllText(path);
            Match m = Regex.Match(text, "^version:\\s*\"?([^\"\\r\\n]+)\"?", RegexOptions.Multiline);
            if (!m.Success) return "contract_version_missing";
            string version = m.Groups[1].Value.Trim();
            return version == ExpectedVersion ? "" : "contract_version_mismatch:" + version;
        }
    }
}
