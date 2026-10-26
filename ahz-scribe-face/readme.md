# ahz-scribe-face

只负责“见”和“说”。

- WinForms / CLI / Web 交互；
- 对话、轨迹、审批单、状态展示；
- 把用户输入变成结构化消息；
- 把 body 的审批请求展示给用户，再把用户决定回传 body。

不直接：

- 执行工具；
- 读写工作区；
- 启停进程；
- 自己批准高危动作。

迁移来源（临时）：`../local-agent/src/AhzScribe.cs` 中的
`MainForm` / `Ui*` / `MsgBlock`。

## v0（控制台壳 + 窗口）

窗口：
```text
AhzScribe.Face.exe --ui
```

简单 WinForms：输出框 + 输入框 + `发送`；`/tool` 前缀走工具，危险工具弹确认框。

已实测：`/ask`、`/tool list_dir`、shell 审批拒绝/允许。

窗口有「对话 / 轨迹 / 记忆 / 设置」四个页；支持 `/remember <kind> <key> <value>` 和 `/memory <query>`；设置页保存模型 Base URL / 模型名 / 工作区 / body 管道到 `_face-data/settings.json`，重启生效；对话用 RichTextBox（TextBox 在 TabPage 下不绘制），工具调用写进轨迹页，最近 30 条记忆写进记忆页。

会话与工具轨迹追加写入：
```text
_face-data/sessions.jsonl
```
重启窗口会恢复最近 200 条。

内部自动分段：
```text
每 40 条消息切一个 episode
state.json 记录当前 episode / message_count / last_user_at / current_focus
```
不提供手动“新建会话”；用户看到的是一条连续时间线。窗口标题会显示当前焦点（截断到 36 字）。

旧 episode 在下次启动时写进 memory.jsonl（key=episode:<id>）。有 DEEPSEEK_API_KEY 时用真实模型生成 1-3 句摘要；否则回退为最近 8 条的确定性摘要。

```text
src/AhzScribe.Face.csproj
src/Program.cs
```

命令：

```text
/remember <kind> <key> <value>
/memory <query>
/ask <text>
/pulse
/quit
```

只调用 brain：

```text
MemoryStore / AgentRunner / PulseScheduler
```

模型选择：
```text
DEEPSEEK_API_KEY / AHZ_MODEL_KEY 存在 → OpenAiToolModel
不存在 → EchoModel
AHZ_MODEL_BASE 默认 https://api.deepseek.com
AHZ_MODEL_NAME 默认 deepseek-flash(1)
```

```text
container: ahz-scribe-face
service:   ahz-scribe-face
```
