using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AhzScribe.Contracts;

namespace AhzScribe.Brain
{
    /// <summary>
    /// Brain-side task runner v0: retrieve memory, call the model, return text.
    /// It deliberately has no tool loop and no body access yet.
    /// </summary>
    public sealed class AgentRunner
    {
        readonly IMemoryStore _memory;
        readonly IBrainModel _model;
        public string BodySummary { get; set; }

        public AgentRunner(IMemoryStore memory, IBrainModel model)
        {
            _memory = memory; _model = model;
        }

        public async Task<string> RunAsync(string userText, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userText)) return "";
            List<MemoryItem> hits = await _memory.RetrieveAsync(userText, 6, ct).ConfigureAwait(false);
            string memory = "";
            if (hits != null && hits.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("相关记忆：");
                foreach (MemoryItem m in hits)
                    sb.AppendLine("- " + (m.Kind ?? "") + " / " + (m.Key ?? "") + " = " + (m.Value ?? ""));
                memory = sb.ToString();
            }
            string system = "你是 Ahz Scribe。只根据用户输入和给出的记忆回答；记忆不足就说明不足。";
            if (!string.IsNullOrEmpty(BodySummary)) system += "\\n" + (BodySummary.Length > 300 ? BodySummary.Substring(0, 300) : BodySummary);
            return await _model.CompleteAsync(system, memory + "\n用户：" + userText, ct).ConfigureAwait(false);
        }
    }
}
