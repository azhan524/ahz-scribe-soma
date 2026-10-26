using System;
using System.Collections.Generic;

namespace AhzScribe.Face
{
    public static class ProactivePolicy
    {
        /// <summary>
        /// S5: a proactive message must share at least one meaningful token with
        /// recent memory. Pure greeting templates are rejected.
        /// </summary>
        public static bool ReferencesMemory(string text, List<AhzScribe.Contracts.MemoryItem> memory)
        {
            if (string.IsNullOrWhiteSpace(text) || memory == null || memory.Count == 0) return false;
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string t in Tokenize(text)) tokens.Add(t);
            if (tokens.Count == 0) return false;
            int hits = 0;
            foreach (AhzScribe.Contracts.MemoryItem m in memory)
            {
                string hay = (m.Key ?? "") + " " + (m.Value ?? "") + " " + (m.Summary ?? "");
                foreach (string t in Tokenize(hay))
                    if (tokens.Contains(t)) hits++;
                if (hits >= 1) return true;
            }
            return false;
        }

        static IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            var current = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || c >= 0x4e00 && c <= 0x9fff)
                    current.Append(c);
                else if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
            if (current.Length > 0) yield return current.ToString();
            if (text.Length >= 2)
                for (int i = 0; i + 2 <= text.Length; i++)
                {
                    char a = text[i], b = text[i + 1];
                    if ((a >= 0x4e00 && a <= 0x9fff) && (b >= 0x4e00 && b <= 0x9fff))
                        yield return text.Substring(i, 2);
                }
        }
    }
}
