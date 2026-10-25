# ahz-scribe-brain

只负责“想”。

- 模型 API 调用 / 流式解析；
- 规划、推理、人格、语气；
- 语义记忆 / 情节记忆的提取与检索；
- 脉冲调度与行动意图；
- 向 body 发送结构化 action-request。

不直接：

- 挂载工作区；
- 执行 shell；
- 读写用户文件；
- 启停进程；
- 写审计终稿（审计由 body 落盘）。

迁移来源（临时）：`../local-agent/src/AhzScribe.cs` 中的
`AgentRunner` / `MemoryStore` / `RuntimeState` / `PulseScheduler`。

## v0（已可构建）

```text
src/MemoryStore.cs      JSONL 记忆 + 关键词/时间检索
src/PulseScheduler.cs   身体状态 + 记忆 + 决策回调，message/none
src/AgentRunner.cs      记忆注入 + 模型调用（无工具循环）
src/ToolAgentRunner.cs  模型提议工具 → 审批 → body 执行 → 结果回模型
src/OpenAiToolModel.cs OpenAI 兼容 chat/tool_calls 客户端（deepseek-v4-pro 实测）
src/MemoryStore.cs       JSONL 记忆
src/PulseScheduler.cs    身体状态 / 脉冲
```

联调：`BRAIN MEMORY/PULSE: PASS`

```text
src/AhzScribe.Brain.csproj
src/BodyClient.cs
src/BrainHost.cs    face 只接触 host；host 拥有 body client / tool loop
```

- 只通过 named pipe `ahz-scribe-body` 调 body；
- 没有文件 / shell / 桌面 API；
- 返回 `ActionResult`。

联调：

```text
BRAIN CLIENT SMOKE: PASS
```

（body 由测试启动，brain 只发 `ActionRequest`，body 执行 `read_file`。）

```text
container: ahz-scribe-brain
service:   ahz-scribe-brain
```
