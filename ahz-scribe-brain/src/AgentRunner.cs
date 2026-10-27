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
        public string UiSummary { get; set; }

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
            string system = "你是 Ahz Scribe。只根据用户输入和给出的记忆回答；记忆不足就说明不足。禁止声称执行了任何没有出现在工具结果或记忆里的动作。你不能直接清除任务、写记忆或改变状态；用户要清任务时应提示发送 /intent-clear，要长期记住信息时应提示发送 /remember <kind> <key> <value>。";
            if (!string.IsNullOrEmpty(BodySummary)) system += "\\n" + (BodySummary.Length > 300 ? BodySummary.Substring(0, 300) : BodySummary);
            if (!string.IsNullOrEmpty(UiSummary)) system += "\\n当前 UI 状态：\\n" + (UiSummary.Length > 600 ? UiSummary.Substring(0, 600) : UiSummary);
            return await _model.CompleteAsync(system, memory + "\n用户：" + userText, ct).ConfigureAwait(false);
        }
    }
}
