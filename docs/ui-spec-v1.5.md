# 硅基生物 UI 规格书 v1.5
## AhzScribe 观察窗 · 可开发版本

> 技术栈：C# WinForms（.NET 10 / .NET Framework 4.8 双栈）+ Scintilla + WebView2（.NET 10）/ CefSharp（仅 .NET Framework 4.8 兼容分支）。.NET 10 需要 Edge WebView2 Runtime。
> 定位：从"设计宣言"补全为"可照着开发的规格书"。v1.0 是主张，v1.1 是规格，v1.2 补齐工程洞，v1.3 统一颜色 token 并修复迭代引入的三处小洞，v1.4 补合并原子性与永久降级出口。
> 核心主张不变：UI 不是功能入口，是存在状态的观察窗。托盘是主要形态，主窗口是观察入口。
> **颜色唯一来源**：所有色值以《硅基生物 UI 配色规划书 v1.1》为唯一权威，本文件仅按区域引用 token，不另设色值。冲突时以配色规划书为准。

---

## 版本记录

| 版本 | 变更 |
|---|---|
| v1.0 | 初版：设计哲学、四区布局、Tab 设计、托盘形态、视觉规范、落地顺序 |
| v1.1 | 补丁 1 故障态矩阵；补丁 2 对话 Tab 保底；补丁 3 渲染三轨收敛为两轨半；补丁 4 数据契约；补丁 5 DPI 与首次托盘引导；新增启动自检 |
| v1.2 | 补丁 6 修复五个工程洞：①审计备用通道（解除自举死循环）；②契约补 version 字段与 `none` 状态；③脉冲 Tab 移入 CefSharp 单实例（解除虚拟模式与可变行高冲突）；④WebView2 降级路径实化为只读快照列表；⑤双栈 DPI 明确 #if 分区方案；附录提示词 1 同步更新 |
| v1.3 | 补丁 7：①颜色 token 全面对齐配色规划书 v1.1（强调蓝 #007ACC→#3794FF、成功绿 #6A9955→#4EC9B0、危险红细分 blocked/error 用 #F14C4C、弱文字统一 text-tertiary #808080、disputed 底统一 bg-disputed #3A3520）；②WebView2 单实例切 Tab 页面状态保留（localStorage 方案）；③fallback 行 UI 标注规格（[备用] 标签）；④BodySensor 重启间隔与重试上限明确；⑤提示词 3 摘要上限放宽至 60 字；⑥审计过滤条 span 补 startup/error；⑦第 7 节改为 token 引用 + 唯一来源声明 |
| v1.4 | 补丁 8：①备用通道合并改为"先写后清"原子化（中途失败备用文件整体保留、下次重试，合并后按 audit_id 去重）；②CefSharp 连续 3 次启动恢复失败进入永久降级模式（停止自动重试，设置 Tab 手动重试退出）；③7.1 token 速查表删除色值列（只留 Token+用途，色值见配色书第 2 节）；④localStorage 键名去掉 `<tab>` 占位符写死三键；⑤展开集合容量防御（>200 条只留最近 200） |
| v1.5 | 补丁 9：正式承认 .NET 10 分支使用 WebView2 替代 CefSharp。修正 3.2/3.3/3.4 渲染载体、5 节渲染分工、5.1 降级路径、提示词 1 的技术栈与桥接方式；WebView2 仅保留在 .NET Framework 4.8 兼容分支。|

---

## 1. 设计原则

### 1.1 观察优先于操作
- 对话不是主界面，状态才是。
- 打开主窗口第一眼是"它现在是什么状态"，不是"输入框在哪"。

### 1.2 安静是正常状态
- 脉冲之间不跳动、不闪烁、不自动刷新。
- "安静"本身是设计语言，不是功能缺失。

### 1.3 事实与解释必须视觉可分
- **事实层**（程序权威）：身体状态、工具结果、审计、记忆条目 → 等宽字体、中性色、无装饰。
- **解释层**（模型判断）：`thought`、`reason`、人格化回复、情绪标签 → 斜体或柔和色、浅色背景、带引号。

### 1.4 不打扰原则
- 只在三种情况弹托盘气泡：主动消息、高风险待确认、急停。
- 其余一切（工具完成、记忆写入、内驱目标）只在窗口可见。

### 1.5 none 必须可见
- 脉冲列表中 `none` 和 `message` 一样可展开。
- "想到但未行动"是生活的一部分，不是错误，不是空状态。

### 1.6 安全模块失效必须降级
- 审计、急停、权限判定任一失效 → Agent 降级为只读，不得带病运行。

### 1.7 UI 失效不影响 Agent
- UI 是观察窗，不是运行时依赖。任何 UI 模块崩溃，主循环照常。

### 1.8 审计必须有备用通道（v1.2 补丁 6）
- 主审计不可写 ≠ 无记录可写。降级与恢复事件必须落在备用通道，否则"审计不可写"这件事本身将无据可查。

---

## 2. 整体布局

```
┌─────────────────────────────────────────────────────────┐
│ 顶栏 BodyStateBar（常驻只读，高 32px）                     │
│ ● 在线 │ 空闲12分 │ 前台 Code.exe │ 磁盘120G │ 14:32:05   │
├──────────────┬──────────────────────────────────────────┤
│              │  Tab 头（自绘，扁平下划线，高 36px）        │
│  左栏        │  [对话] [脉冲] [记忆] [审计] [任务] [设置] │
│  StatePanel  ├──────────────────────────────────────────┤
│  宽 260px    │                                          │
│              │  内容区（随 Tab 切换）                    │
│  · 当前焦点  │                                          │
│  · 未完成意图│                                          │
│  · 身体摘要  │                                          │
│  · 最近脉冲  │                                          │
│  · 情绪标签  │                                          │
│              │                                          │
├──────────────┴──────────────────────────────────────────┤
│ 底栏 ActionBar（高 56px）                                 │
│ [输入框.............................] [⏸] [📋] [■急停]  │
└─────────────────────────────────────────────────────────┘
```

### 2.1 窗口尺寸
- 默认 1280×800，最小 1024×680。
- 左栏固定 260px，主区域弹性。
- 关闭按钮 = 缩到托盘，不是退出。
- 退出走托盘右键菜单，需二次确认。

### 2.2 顶栏 BodyStateBar
- 高 32px，底 `bg-primary`，字 `text-primary`，分隔符 `text-secondary`。
- 左侧状态圆点：灰=休眠（`state-neutral`），蓝呼吸=脉冲中（`state-info`，呼吸插值 `state-info-dim`↔`state-info`），黄=待确认（`state-warning`），红=急停（`state-danger`）。
- 字段：连接 / 空闲时长 / 前台应用 / 磁盘剩余 / 时钟。
- 采样跟随 `BodySensor`（30s）。数字直接替换，不做动画。
- 点击无菜单，纯只读。

### 2.3 左栏 StatePanel
- 宽 260px，底 `bg-secondary`，卡片无边框，卡片间 1px `border-subtle`。
- 卡片从上到下：
  1. 当前焦点（`current_focus`，加粗）
  2. 未完成意图（最多 3 条，超出显示 "+N 条"）
  3. 身体摘要（空闲、前台、网络三项精简）
  4. 最近脉冲（最近 3 次：时间 + 行动）
  5. 情绪标签（`mood_label`，单行 + 小圆点）
- 卡片只读，跳转在 Tab 里做。
- 左栏整体滚动，卡片不单独滚动。

### 2.4 Tab 头
- 自绘，不用 `TabControl` 原生头。
- 高 36px，底 `bg-tertiary`。
- 选中：白字（`text-primary`）+ 底部 2px `state-info` 下划线。
- 未选中：`text-secondary`，无下划线。
- 顺序：对话 / 脉冲 / 记忆 / 审计 / 任务 / 设置。
- 脉冲 Tab 有未读时，Tab 文字后加 `state-info` 小圆点（4px），不改 Tab 颜色。

### 2.5 底栏 ActionBar
- 高 56px，底 `bg-primary`，顶边 `border-subtle` 1px。
- 输入框：占满剩余宽度，多行自适应（最高 120px），无边框，底 `bg-secondary`，圆角 4px；聚焦边框 `border-strong`。
- 右侧三按钮：
  - **⏸ 暂停主动**：弹三级菜单（2 小时 / 直到明天 / 取消），无确认框。
  - **📋 查看审计**：跳审计 Tab 并定位最近一条。
  - **■ 急停**：`DangerButtonBg` 底 + `DangerButtonFg` 文字，单击即停，**不弹确认**。任何时候可见。

---

## 3. 各 Tab 详细设计

### 3.1 对话 Tab

对话占 90% 使用时间，必须有保底规格。

**消息列表**：用户右对齐（气泡 `bg-elevated`），Agent 左对齐（气泡 `bg-secondary`）。Agent 消息如含 `thought` 或工具调用，用浅色折叠块（`bg-tertiary`），默认折叠，点击展开，字号小 1px。

**消息状态机**（每条 Agent 消息必须有明确状态）：

| 状态 | 视觉 | 触发 |
|---|---|---|
| `sending` | 气泡右侧静态灰点 | 已发往 LLM |
| `streaming` | 文字逐字出现，末尾静态光标 `▌` | 流式返回中 |
| `done` | 无装饰 | 流式结束 |
| `waiting_tool` | 气泡下方灰字"正在执行：{tool}" | 等工具返回 |
| `error` | 气泡右侧红点（`state-danger`），点击展开错误摘要 | 超时/失败 |
| `none` | 不产生消息实体，只在脉冲 Tab 可见 | 脉冲选 none |

> v1.2 注：`none` 不产生 `ConversationMessage` 实体（见第 8 节契约，status 含 `none` 仅为对齐枚举，UI 层不渲染该消息）。

**关键：不做"正在输入"动画，但必须有"最后响应时间"**。对话 Tab 右上角固定显示 `最后响应：14:32:05（3 分钟前）`（`text-tertiary`），每次消息进入 `done` 或 `error` 时更新。用户切回 Tab 一眼判断"它是不是还活着"。

**输入框行为**：
- 脉冲执行中**不禁用**，消息进队列，当前脉冲结束后按 `user_message` 优先级处理。
- 队列中待处理消息在输入框上方显示"1 条待发送"。
- 急停状态下输入框禁用，placeholder 显示"已急停"。

**对话与脉冲互链**：`done` 的 Agent 消息右下角有 `→ pulse_xxx` 链接；脉冲 Tab 中 `message` 行动条目有 `→ 对话` 链接。

### 3.2 脉冲 Tab（核心面板）

**实现载体（v1.2 修订）**：**HTML + WebView2，与记忆/审计共用同一实例**。原因：展开态是可变高度的富内容块（state_restore / thought / action / state_writeback），`DataGridView.VirtualMode` 要求等行高，无法承载"行内撑开"；HTML 的懒渲染与过滤天然适配。切到脉冲 Tab 时通过 `LoadHtml`/本地路由切换内容，不新建实例。

**页面状态保留（v1.3 补丁 7 + v1.4 补丁 8）**：切 Tab 换内容会让前页的滚动位置、过滤条件、展开行全部丢失（v1.2 引入的退化）。**实作要求：每个 HTML 页面把 UI 状态写入 `localStorage`，页面加载时恢复**：
- 保存项：滚动位置（`scrollTop`）、过滤条件（下拉选中值 + 搜索词）、展开的 `pulse_id` 集合。
- 存储键：写死 `ahzscribe.ui.pulse` / `ahzscribe.ui.audit` / `ahzscribe.ui.memory` 三键，各页只读写自己的键，**不拼 tab 名**（单用户场景下无区分意义，去掉占位符避免实现歧义）。
- 展开集合容量防御：写入前截断，超过 200 条只保留最近 200 条（按时间序）。
- 恢复时机：`DOMContentLoaded` 后、首次渲染数据前恢复过滤条件，数据渲染完成后恢复滚动位置和展开集合。
- 不保存：大数据集本身（只存状态，不存数据，数据每次从 C# 桥接拉取）。
- 不实现 localStorage 时的降级：页面照常可用，只是切回时状态重置（不视为 bug，但必须在代码注释中说明）。

**布局**：时间倒序，每条一行，可展开。

**折叠态**：
```
14:32:05  user_message   → 回复了"好的"                        [▸]
14:05:12  body_event     → none（想到：该问文件确认了）          [▸]
13:40:00  drive          → memory（记录了"用户偏好暖光"）        [▸]
12:15:30  timer          → none（静默时段，不主动）              [▸]
```

**视觉**：
- 时间：等宽，`text-secondary`。
- 触发源标签：按配色书 2.4 表取色（`user_message`=state-info / `body_event`=state-success / `drive`=state-drive / `timer`=state-neutral / `intent_due`=state-warning），纯文字标签。
- 行动：`message`/`tool` 用 `text-primary`；`none` 用 `text-tertiary`（可读暗色，不是占位）；`memory` 用 `state-success`。

**展开态**：
```
┌─ state_restore ──────────────────────────────┐
│ last_user_contact_at=14:20:00                 │
│ current_focus="整理记忆"                      │
│ open_intents=["intent_001"]                   │
└───────────────────────────────────────────────┘

┌─ thought（解释层，斜体，bg-tertiary 底）─────┐
│ "主人刚才说'好的'，语气很简短，可能在忙。"    │
└───────────────────────────────────────────────┘

┌─ action（事实层，等宽）──────────────────────┐
│ none                                          │
│ reason: "用户在忙，选择不打扰"                │
│ confidence: 0.82                              │
└───────────────────────────────────────────────┘

┌─ state_writeback（事实层）───────────────────┐
│ last_pulse_result=none, mood=平静             │
└───────────────────────────────────────────────┘

audit_id: aud_20261024_140512_ab12   [查看审计 →]
```

**过滤条**（顶部，高 40px）：
- 触发源下拉 / 行动下拉 / 时间范围（今天/7天/全部）/ 全文搜索（搜 `thought` 和 `reason`）。

**none 可见性**：过滤"全部行动"时，`none` 不隐藏、不淡化到不可读。

**数据量大时的滚动**：HTML 侧 `IntersectionObserver` 懒渲染，与审计 Tab 同一套实现。

### 3.3 记忆 Tab

**两个子 Tab**：语义记忆 / 情节记忆。**用 WebView2 单实例渲染**（与脉冲、审计共用）。

**语义记忆（表格）**：
- 列：`key` / `value` / `confidence` / `status` / `来源` / `时间`。
- `status=disputed` 行：底 `bg-disputed` + 左边框 3px `state-warning`。
- 点击展开：完整 JSON、`source_audit_id`、关联 `disputed` 条目。
- 顶部按 key/value 模糊搜索。

**情节记忆（时间线）**：
- 每条卡片垂直堆叠（底 `bg-secondary` + `border-subtle` 1px），显示 `summary`（加粗）+ `emotion` 标签 + `status` 徽章（open=`state-success` / closed=`text-tertiary` / disputed=`state-warning`）+ 时间。
- 点击展开完整字段、`people`、`source_audit_id`。
- `disputed` 卡片左边框 3px `state-warning`。

**共用**：每条右下角 `→ aud_xxx` 链接（`state-info`），跳审计 Tab 对应条目。

### 3.4 审计 Tab

**用 WebView2 单实例渲染**（与脉冲、记忆共用，切 Tab 只换内容）。

**过滤条**：`span` 下拉（全部 / tier_decision / approval / tool_call / blocked / snapshot / rollback / pulse / resume / error / startup——v1.3 补齐全部枚举）/ `decision` 下拉 / 时间范围（今天/7天/全部）/ 按 tool 名或 `args_summary` 搜索。

**表格**（默认最近 50 条）：
```
时间        span          tool         decision    result   snapshot
14:32:05    tool_call     write_file   allowed     ok       snap_xxx
14:31:50    approval      -            approved    -        -
14:31:48    tier_decision -            workspace   -        -
```

- 行高 28px，列宽可调。
- `decision=blocked` 行左边框 3px `state-danger`（整行不变色）。
- `approved`/`ok` 文字 `state-success`；`rejected`/`error` 文字 `state-danger`；`pending` 文字 `state-warning`。
- **`fallback: true` 行（v1.3 补丁 7）**：时间列后加 `state-warning` 的 `[备用]` 文字标签，不加行底色、不加左边框。与 `blocked` 的"小面积标注"哲学一致。悬停标签显示备用文件路径 `audit_fallback.jsonl`。
- 点击展开：完整 JSON、`payload_ref` 链接、`linked_audit_id` / `rolled_back_by` 跳转。
- 数据量大时用虚拟滚动（HTML 侧 `IntersectionObserver` 懒渲染）。

### 3.5 任务 Tab
- 显示 `active_task`、后台任务列表、`open_intents` 监控状态。
- 每条：类型、开始时间、进度（可获取时）、状态。
- 点击展开：关联工具调用序列、关联审计条目。
- 空状态："当前无活跃任务" + 小字"最近一次任务：xxx"。

### 3.6 设置 Tab
- 分组：LLM / 权限 / 记忆 / 脉冲 / 主动性 / 审计 / 人格。
- 就地编辑，不弹模态框。
- `SOUL.md` / `BOUNDARIES.md` 只读，旁边"打开文件"链接。
- 改 `STYLE.md` 弹"修改需人工确认"小提示，确认后写入。

---

## 4. 故障态设计（补丁 1 + v1.2 修订 + v1.3 修订）

| 故障 | UI 表现 | Agent 行为 | 恢复路径 |
|---|---|---|---|
| **急停触发** | 顶栏圆点红常亮（`state-danger`）；底栏急停变"恢复"；所有 Tab 顶部黄色横幅"已急停，脉冲停止" | 所有脉冲拒绝执行；`EMERGENCY_STOP` 存在时 `RunPulse` 入口直接 return | 点"恢复" → 二次确认 → 删文件 → 写 `span=resume` 审计 → 圆点回灰 |
| **WebView2 崩溃** | 脉冲/记忆/审计 Tab 显示兜底页"此面板暂不可用，点击重试"；对话/任务/设置不受影响 | 不影响主循环；HTML 查询走独立桥接，崩溃不阻塞 C# | "点击重试"销毁重建实例；连续 3 次失败 → 切换**只读快照列表**（见 5.1） |
| **UIStateBus 断开** | 顶栏加"数据延迟"小标签显示最后更新时间；所有面板停止增量更新，保留最后快照 | 照常运行，审计照常写 | 重连后全量拉 `current_state` + `body_state` + 最近 50 条脉冲，覆盖式刷新 |
| **BodySensor 线程死亡** | 顶栏空闲/前台/磁盘变灰显示"--"（`text-disabled`） | 脉冲仍可运行，但 `body_event` 触发源失效 | **v1.3 明确**：主程序每 30 秒心跳检测一次线程存活，死亡后立即重启；连续 3 次重启失败 → 写 `span=error` 审计 + 顶栏标红（`state-danger`） |
| **LLM 不可达** | 对话当前消息 `error` 状态；脉冲条目标 `error` | 超时后走 `none` + `reason=llm_unreachable`；不无限重试 | 指数退避重试 3 次；失败后顶栏圆点变黄（`state-warning`）"大脑离线"，只留本地小脑脉冲 |
| **审计写入失败** | 底栏红色"审计不可写" | **立即降级只读**：所有 `workspace_write` 及以上拒绝，只允许 `read_only`；降级/恢复事件写备用通道 | 人工修权限后点"恢复审计"；恢复后把备用通道记录合并回主审计 |
| **托盘图标创建失败** | 主窗口关闭按钮变"最小化"；启动时弹一次气泡说明 | 不影响 Agent | 用户手动最小化；下次启动重试 |

### 4.1 审计备用通道（v1.2 补丁 6 + v1.3 修订）

**问题**：v1.1 要求"审计不可写时，降级/恢复各写一条审计到'降级前最后可用通道'"、启动自检写 `span=startup` 审计——但主审计不可写时这些记录无处可落，构成逻辑死结。

**定义（实作）**：
- 独立备用文件：`audit_fallback.jsonl`。
- **放置位置（v1.3 修订）**：**不放在主审计目录**。主审计目录整体不可写（挂载只读、整盘权限丢失）时，同目录备用通道会一起死。备用通道放 `%LOCALAPPDATA%\Ahz\audit_fallback\audit_fallback.jsonl`，与主审计物理隔离。
- 写入策略：主审计写入失败时，审计组件自动切换 `AuditWriter` 的落盘目标到备用文件，并重试主文件（指数退避，每 5 分钟探测一次）。
- 内容：与主审计同 schema（见第 8 节 `AuditEntry`），额外带 `fallback: true` 标记。
- 恢复合并（**v1.4 补丁 8：先写后清，原子化**）：`AuditWriter` 恢复主文件可用后，把 `audit_fallback.jsonl` 中未合并条目按时间序**全部**写入主文件；**确认全部写入成功后，才清空备用文件**。合并中途主文件再次失败时：**备用文件保持完整、一条不清**，下次恢复时整体重试（不做"边写边清"）。主文件在合并窗口内可能短暂存在与备用文件的重复条目，**合并成功后按 `audit_id` 去重**（写入前先过滤主文件已存在的 `audit_id`）。合并完成后写一条 `span=resume` 审计。**这是审计链唯一可能"丢数据或重复数据"的环节，实现必须按上述顺序执行，不得优化成边写边清。**
- UI 表现：备用通道启用时，底栏"审计不可写"标签旁加"记录于备用通道"小字，指向备用文件路径（点击打开）；备用通道的条目在审计 Tab 显示 `[备用]` 标签（见 3.4）。
- 启动自检：审计目录不可写时，`span=startup` 审计同样写备用通道；备用通道也不可写时，才真正无记录（此情况下顶栏标红并弹一次警告）。

**两条硬规则**：
1. 安全模块（审计/急停/权限判定）失效 → Agent 降级只读，不带病运行。
2. UI 模块失效 → Agent 主循环不受影响。UI 是观察窗，不是运行时依赖。

---

## 5. 渲染分工（补丁 3 + v1.2 修订 + v1.3 修订）

| 区域 | 技术选择 | 理由 |
|---|---|---|
| 顶栏、左栏、Tab 头、底栏 | WinForms 自绘 | 需实时同步，自绘量小 |
| 对话、任务 | WinForms 原生控件 | 对话需要和 C# 状态实时同步；任务结构简单 |
| 脉冲、记忆、审计 | **HTML + WebView2，共用一个实例** | 脉冲展开态是可变高度富内容，虚拟列表无法承载；记忆/审计本来就是富交互 |
| 设置 | WinForms 原生 | 简单表单，不值得上 HTML |

**关键收敛**：
- **WebView2 全局只开一个实例**，脉冲、记忆、审计共用，通过 `LoadHtml` 或本地路由切换内容。切 Tab 不销毁实例。
- **页面状态保留**（v1.3）：切 Tab 换内容前，各页把 UI 状态写入 `localStorage`（滚动/过滤/展开集合），切回时恢复——见 3.2 节规格。
- **HTML 页面总数 ≤ 3**（脉冲、记忆、审计），同一实例内切换。
- **脉冲列表不落在 `DataGridView.VirtualMode` 上**（虚拟模式要求等行高，与行内展开冲突，已移入 HTML）。
- 不引入第三套渲染技术。

**JS 桥接**：WebView2 `AddHostObjectToScript` 暴露 `host` 对象，HTML 通过 `host.queryPulses(filter)` / `host.queryAudit(filter)` / `host.queryMemories(filter)` 调用 C#，避免大量数据塞进 `boundObject`。

**维护成本声明**：渲染路径只有两条——WinForms（自绘 + 原生）和 WebView2（单实例三页面）。

### 5.1 WebView2 降级路径（v1.2 补丁 6 实化）

**问题**：v1.1 写"连续 3 次失败降级到原生表格"，但原生表格全篇无设计，等于纸面兜底。

**实作（只读快照列表）**：
- 触发：同一实例连续 3 次启动失败（或 5 分钟内 3 次崩溃）。
- 表现：脉冲/记忆/审计 Tab 顶部显示黄色横幅"HTML 面板不可用，已切换为只读列表模式"。
- 实现：C# 侧用只读 `ListBox`（OwnerDraw）或只读 `DataGridView`（非虚拟模式，最多加载最近 200 条），按契约字段渲染平铺文本行：
  - 脉冲行：`HH:mm:ss trigger → action（摘要）`，点击展开用旁边只读文本框显示完整 JSON。
  - 审计行：`HH:mm:ss span tool decision result`，`fallback=true` 行追加 `[备用]` 文本；点击显示完整 JSON。
  - 记忆行：`summary [status] emotion`。
- 明确不做：过滤、搜索、跳转、分页——只保证**可读**，不保证**好用**。
- 退出条件：下次启动时自动尝试恢复 WebView2 实例，成功即退出只读列表模式，并在审计写 `span=resume`。
- **永久降级模式（v1.4 补丁 8）**：连续 **3 次启动恢复失败**后，不再每次启动自动尝试恢复（否则"每次启动都进降级、每次尝试都失败"形成死循环）。进入永久降级：降级状态持久化写入 `config.json`（`ui_state.html_disabled=true`），设置 Tab 显示"HTML 面板不可用，点击手动重试"；手动重试成功即退出永久降级并清除该标志，恢复正常模式。

---

## 6. 托盘形态（主要驻留方式）

### 6.1 托盘图标
- 小圆点，颜色随状态：灰 `state-neutral` 休眠 / 蓝 `state-info` 呼吸脉冲中 / 黄 `state-warning` 待确认 / 红 `state-danger` 急停。
- 呼吸周期 2-3 秒（`state-info-dim`↔`state-info` 实色插值），不闪烁。
- 尺寸 16×16，高 DPI 32×32、48×48，提供三套 PNG，按 DPI 选择，不靠 GDI+ 缩放。

### 6.2 托盘右键菜单
```
打开 AhzScribe
─────────────
当前状态：空闲中
最近脉冲：2 分钟前（none）
─────────────
暂停主动（2 小时）
查看审计
─────────────
急停
退出
```
- "当前状态""最近脉冲"只读。
- "暂停主动"弹三级菜单（2 小时 / 直到明天 / 取消）。
- "急停"单击即停，无确认。
- "退出"需二次确认（"确定退出？后台脉冲将停止"）。

### 6.3 托盘气泡
只三种情况弹：
1. 主动消息 → 气泡为消息前 50 字。
2. 高风险待确认 → 操作摘要 + "点击查看"。
3. 急停触发 → "已急停"。

**其余不弹。这是硬规则，不因调试方便放宽。**

### 6.4 首次托盘引导（补丁 5）
1. 首次启动，主窗口正常显示，底部弹**非模态**提示条："关闭窗口后，AhzScribe 会缩到托盘继续运行。托盘图标在任务栏右下角。" 右侧"知道了"按钮，点击写 `config.json` 的 `first_run_tray_hint_shown=true`。
2. 首次点关闭按钮时若标志仍为 false，不关闭，弹**模态**确认框："关闭后将缩到托盘。是否继续？"带"不再提示"复选框。
3. 托盘图标首次创建时，若系统托盘默认折叠（Win11 隐藏图标区），弹一次气泡："我在右下角的隐藏图标里，可拖出固定。"

### 6.5 启动自检（每次启动跑，结果写 `span=startup` 审计）
- 审计目录可写？不可写 → 只读模式启动，顶栏标红，`span=startup` 写**备用通道**（见 4.1）。
- `config.json` 存在且 schema 版本匹配？不匹配 → 弹窗，不进入主循环。
- 托盘图标能创建？失败 → 禁用"关闭到托盘"，关闭按钮变"最小化"。
- `BodySensor` 线程能启动？失败 → 顶栏身体字段置灰（`text-disabled`），记录 error 审计。

---

## 7. 视觉规范（v1.3：token 引用版）

> **颜色唯一来源声明**：所有色值以《硅基生物 UI 配色规划书 v1.1》为唯一权威。本表仅列本文件引用的常用 token，组件开发一律引用 token 名，不得硬编码色值；两份文档冲突时以配色规划书为准。

### 7.1 常用 token 速查（v1.4：仅 Token + 用途，色值见配色书第 2 节）

> 本表不列色值——**色值以《硅基生物 UI 配色规划书 v1.1》第 2 节 token 表为唯一来源**。组件开发一律引用 token 名；本文件任何位置不出现裸色值，避免出现第二个真相来源。

| Token | 本文件用途 |
|---|---|
| `bg-primary` | 窗口底、顶栏底、底栏底 |
| `bg-secondary` | 左栏、输入框、气泡 |
| `bg-tertiary` | Tab 头、折叠块 |
| `bg-elevated` | hover、选中行、用户气泡 |
| `bg-disputed` | disputed 行底 |
| `border-subtle` | 分割线 |
| `border-strong` | 输入框聚焦 |
| `text-primary` | 正文 |
| `text-secondary` | 辅助、时间戳 |
| `text-tertiary` | `none`、弱提示 |
| `text-disabled` | 禁用态 |
| `state-info` | 强调、链接、选中、user_message |
| `state-info-dim` | 呼吸暗端插值色 |
| `state-success` | 成功、memory |
| `state-warning` | disputed、待确认、[备用] 标签 |
| `state-danger` | blocked、error、危险圆点 |
| `state-drive` | drive 标签 |
| `state-neutral` | timer、休眠 |
| `DangerButtonBg` | 急停按钮底（配色书专用 token） |
| `DangerButtonFg` | 急停按钮文字（全界面唯一纯白） |

**颜色只用于语义，不用于装饰。** 任何颜色出现都对应一个明确的状态含义。

### 7.2 字体
- 事实层：`Consolas` / `Cascadia Mono`，等宽，9-10pt。
- 解释层：`Microsoft YaHei UI` / `Segoe UI`，斜体，10pt。
- 普通：`Microsoft YaHei UI`，10pt。
- 行高 1.4 倍。

### 7.3 间距
- 卡片内边距 12px，卡片间 8px。
- 列表行高 28px，Tab 头内边距 16px。
- 全局 4 的倍数。

### 7.4 DPI 缩放（补丁 5 + v1.2 修订 + v1.3 修订）

- 所有尺寸标注为 **96 DPI 逻辑像素**，渲染时按 `Graphics.DpiX / 96.0` 缩放。
- 自绘部分坐标和字号乘 `scaleFactor`，不硬编码像素。
- WebView2 设 `CoreWebView2Controller 的 RasterizationScale / ShouldDetectMonitorScaleChanges`，HTML 用 `rem` + `viewport`，不用固定 px。
- 托盘图标按 DPI 选 PNG（16/32/48），不做 GDI+ 缩放。
- **最低系统要求（v1.3 补丁 7）**：PerMonitorV2 需要 Windows 10 1703+ 完整支持；4.8 分支在 Win10 1703 以下自动退化为 System DPI，功能不受影响，缩放平滑度略降。

**双栈 DPI 分区（v1.2 补丁 6）**：

`.NET 10` 与 `.NET Framework 4.8` 的 HighDpi 设置 API 不同，统一用 `#if` 分区：

```csharp
// Program.cs / Main
#if NET10_0_OR_GREATER
    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#else
    // .NET Framework 4.8：无 SetHighDpiMode API，
    // 改为在 app.config 声明（见下方 App.config 片段）
#endif
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
```

```xml
<!-- App.config（.NET Framework 4.8 分支使用） -->
<configuration>
  <System.Windows.Forms.ApplicationConfigurationSection>
    <add key="DpiAwareness" value="PerMonitorV2" />
  </System.Windows.Forms.ApplicationConfigurationSection>
</configuration>
```

> 注意：`#if NET10_0_OR_GREATER` 需要项目里定义 `NET10_0_OR_GREATER` 符号；.NET Framework 4.8 分支不定义该符号，走 `#else`。若 SDK 风格项目自动生成符号，需确认 4.8 目标下未错误启用。

---

## 8. 数据契约（补丁 4 + v1.2 修订）

所有 UI、提示词、C# 代码以本契约为准。**C# 侧 `UiDataMapper` 把原始 JSONL 映射成契约结构，UI 只认契约。**

```yaml
# ui_data_contract.yaml v1.2

version: "1.2"          # 契约版本号，C# 启动时校验，不匹配拒绝启动

PulseSummary:
  pulse_id: string       # pulse_YYYYMMDD_HHMMSS_xxxx
  ts: string             # ISO8601 带时区
  trigger_type: enum[user_message, body_event, drive, timer, intent_due]
  action: enum[message, tool, memory, wait, continue, none, finish]
  summary_line: string   # ≤60 中文字符（v1.3：由 40 放宽，见提示词 3）
  audit_id: string

PulseDetail:
  extends: PulseSummary
  state_restore: object
  thought: string|null
  reason: string|null
  confidence: float|null
  tool_calls: list[ToolCall]
  state_writeback: object
  memories_injected: list[string]

ToolCall:
  tool: string
  args_summary: string   # 脱敏，不含敏感值
  result: enum[ok, error, blocked, pending]
  duration_ms: int|null
  audit_id: string

BodyStateSummary:
  sampled_at: string
  online: bool
  idle_seconds: int
  foreground_app: string
  disk_free_gb: float
  network_online: bool
  emergency_stop: bool

CurrentStateSummary:
  current_focus: string
  mood_label: string
  open_intents: list[IntentSummary]  # 最多 3 条
  open_intents_total: int
  last_pulse: PulseSummary|null
  active_task: TaskSummary|null
  pending_confirmations: int

IntentSummary:
  intent_id: string
  condition: string      # ≤30 字摘要
  status: enum[monitoring, due, done, cancelled]
  risk: enum[low, medium, high]

MemoryEntry:
  mem_id: string
  type: enum[semantic, episodic]
  summary: string
  status: enum[active, disputed, closed]
  confidence: float|null
  emotion: string|null
  source_audit_id: string
  ts: string

AuditEntry:
  audit_id: string
  ts: string
  span: enum[tier_decision, approval, tool_call, blocked, snapshot, rollback, pulse, resume, error, startup]
  tool: string|null
  decision: enum[allowed, blocked, approved, rejected, pending]|null
  result: enum[ok, error, null]|null
  snapshot_id: string|null
  linked_audit_id: string|null
  rolled_back_by: string|null
  args_summary: string|null
  fallback: bool = false   # 仅备用通道记录为 true

ConversationMessage:
  msg_id: string
  role: enum[user, agent]
  content: string
  status: enum[sending, streaming, done, waiting_tool, error, none]  # none 对齐状态机；UI 不渲染 none 消息实体
  last_response_at: string|null
  linked_pulse_id: string|null
```

**三条使用规则**：
1. UI 只消费契约字段，不直接读原始 JSONL。
2. 提示词引用的字段名必须来自契约，不自造。
3. 契约带 `version` 字段，C# 启动时校验版本，不匹配拒绝启动。

---

## 9. 数据到 UI 的映射

| UI 元素 | 数据源 | 刷新时机 |
|---|---|---|
| 顶栏圆点 | `current_state.emergency_stop` / `active_pulse` / `pending_confirmations` | 状态变更事件 |
| 顶栏空闲/前台/磁盘 | `body_state.json` | `BodySensor` 每次采样后 |
| 左栏当前焦点 | `current_state.current_focus` | 脉冲写回时 |
| 左栏未完成意图 | `open_intents.json` | 意图变更事件 |
| 左栏最近脉冲 | `pulse_log.jsonl` 最后 3 条 | 新脉冲写入时 |
| 左栏情绪标签 | `current_state.mood_label` | 脉冲写回时 |
| 脉冲 Tab | `pulse_log.jsonl` | 打开加载，新脉冲追加 |
| 记忆 Tab | `semantic.jsonl` / `episodic.jsonl` | 打开加载，写入增量更新 |
| 审计 Tab | `audit.jsonl`（主） + `%LOCALAPPDATA%\Ahz\audit_fallback\audit_fallback.jsonl`（备用通道） | 打开加载最近 50 条，分页；备用通道条目标注 `[备用]` |
| 任务 Tab | `current_state.active_task` / 后台任务表 | 任务状态变更事件 |
| 托盘图标颜色 | 同顶栏圆点 | 状态变更事件 |
| 托盘菜单状态文字 | `current_state` + `pulse_log` 最后一条 | 菜单打开时 |

**统一事件总线**：所有面板订阅 `UIStateBus`，状态变更时统一广播。**不允许面板自己轮询文件。**

---

## 10. 落地顺序

| 步骤 | 内容 | 验收 |
|---|---|---|
| **U1** | 顶栏 + 托盘图标 + 托盘菜单 + 首次托盘引导 + 启动自检（含备用通道） | 关窗后托盘驻留；顶栏和图标随 `body_state` 更新；急停可触发；首次启动有引导；审计不可写时降级只读且备用通道有记录 |
| **U2** | 脉冲 Tab（HTML，折叠 + 展开 + 过滤 + localStorage 状态保留）+ 对话 Tab 保底（状态机 + 最后响应时间 + 队列） | 能读出最近几小时"做了什么、想了什么、什么时候选 none"；对话有明确状态；切 Tab 再切回，过滤和展开状态不丢 |
| **U3** | 审计 Tab（WebView2 单实例，表格 + 过滤 + 展开 + 备用通道合并 + [备用] 标签） | 任一工具调用能查到完整链，`blocked` 行有红线；备用通道条目带 [备用] 标签可区分 |
| **U4** | 左栏 + 底栏 + 故障态兜底（含只读快照列表） | 左栏随脉冲更新；急停即停不确认；WebView2 崩溃有兜底且三次后进只读列表 |
| **U5** | 记忆 Tab（与脉冲、审计共用 WebView2 实例） | 记忆卡片能跳审计；disputed 行显示 bg-disputed 底 + 黄边框 |
| **U6** | 任务 Tab + 设置 Tab + 数据契约校验 | 活跃任务显示进度；设置就地编辑；契约版本不匹配拒绝启动 |

**U1 + U2 做完即可验收"硅基生物 UI 是否成立"**：关掉主窗口只留托盘，过几小时再打开，从顶栏和脉冲日志能读出"它这几个小时干了什么、想了什么、什么时候选择不打扰你"——读得出，UI 就成立。

---

## 11. 验收清单

### 观察性
- [ ] 打开主窗口第一眼看到"它现在是什么状态"。
- [ ] 脉冲日志能读出最近 24 小时的"生活节律"。
- [ ] `none` 脉冲可见且可展开。
- [ ] 脉冲展开能区分事实层和解释层。

### 安静性
- [ ] 无新脉冲时不跳动、不闪烁。
- [ ] 工具完成、记忆写入、内驱目标生成不弹托盘气泡。

### 可追责性
- [ ] 每条记忆能跳审计。
- [ ] 每条脉冲展开有 `audit_id` 链接。
- [ ] 审计 `blocked` 行有红线；`rolled_back_by` 能跳转。

### 托盘
- [ ] 关主窗口不退出程序。
- [ ] 图标颜色随状态变化，呼吸不快于 2 秒/周期。
- [ ] 急停按钮底栏任何时候可见。
- [ ] 托盘"退出"需二次确认。
- [ ] 首次启动有托盘引导；首次关闭弹模态确认。
- [ ] 系统托盘默认折叠时弹一次引导气泡。

### 故障态（补丁 1 + v1.2 + v1.3）
- [ ] 急停后顶栏红点常亮，底栏出现"恢复"，恢复需二次确认。
- [ ] 手动杀 WebView2 子进程，脉冲/记忆/审计显示兜底页，对话/任务不受影响。
- [ ] 连续 3 次 WebView2 失败后进入只读快照列表，横幅提示，下次启动自动尝试恢复；连续 3 次启动恢复失败后进入永久降级模式，不再自动重试，设置 Tab 显示"点击手动重试"，手动重试成功即恢复。
- [ ] 手动停 `BodySensor` 线程，顶栏身体字段变灰，30 秒内自动重启；连续 3 次重启失败写 `span=error` 并顶栏标红。
- [ ] 审计目录设只读，Agent 降级只读模式，写操作被拒，降级/恢复记录出现在备用通道（`%LOCALAPPDATA%\Ahz\audit_fallback\`）。
- [ ] 恢复审计可写后，备用通道记录先全部写入主审计、确认成功后备用文件才清空；合并中途主文件再次失败时备用文件保持完整，恢复后整体重试；合并完成后按 `audit_id` 去重，无丢条、无重复条。
- [ ] 断网后对话消息 `error`，顶栏圆点变黄，本地小脑脉冲仍可运行。

### 对话保底（补丁 2）
- [ ] 每条 Agent 消息有明确状态。
- [ ] 对话 Tab 右上角显示"最后响应时间"。
- [ ] 脉冲执行中输入框不禁用，消息进队列显示"N 条待发送"。
- [ ] 对话消息和脉冲条目可双向跳转。

### 渲染收敛（补丁 3 + v1.2 + v1.3）
- [ ] WebView2 全局只有一个实例，脉冲/记忆/审计切换不重建。
- [ ] 脉冲列表为 HTML 懒渲染，非 DataGridView 虚拟模式。
- [ ] HTML 页面总数 ≤ 3。
- [ ] 切 Tab 再切回，滚动位置、过滤条件、展开集合保留（localStorage）。

### 数据契约（补丁 4 + v1.2）
- [ ] `ui_data_contract.yaml` 存在且带 `version` 字段。
- [ ] C# 启动校验契约版本，不匹配拒绝启动。
- [ ] 四个提示词引用的字段名全部能在契约中找到。

### 颜色 token（v1.3 + v1.4）
- [ ] 规格书正文、速查表与示例中无裸色值，全部引用配色规划书 v1.1 的 token（7.1 速查表仅列 Token+用途；版本记录中的色值仅作历史变更轨迹，不作开发引用）。
- [ ] 强调蓝统一 `state-info`；成功绿统一 `state-success`；blocked/error 用 `state-danger`；急停按钮用 `DangerButtonBg`。
- [ ] `fallback` 行显示 `state-warning` 的 `[备用]` 标签，不加行底色。

### DPI 与首次引导（补丁 5 + v1.2 + v1.3）
- [ ] 150% 和 200% DPI 下顶栏/左栏/Tab/底栏无重叠、无截断。
- [ ] .NET 10 走 `SetHighDpiMode`；.NET Framework 4.8 走 app.config 声明，两栈均生效；Win10 1703 以下退化 System DPI。
- [ ] 首次启动出现托盘引导提示条，点"知道了"不再出现。
- [ ] 首次关闭弹模态确认，带"不再提示"。
- [ ] 启动自检结果写入 `span=startup` 审计（审计不可写时写入备用通道）。

---

## 12. 反模式清单

- ❌ 把对话做成主界面占满全屏。
- ❌ 给脉冲列表加"正在加载"转圈。
- ❌ 用颜色装饰而非表达语义。
- ❌ `none` 脉冲用灰色占位或隐藏。
- ❌ `thought` 和工具结果用同一字体。
- ❌ 托盘气泡当通知中心用。
- ❌ 急停按钮弹二次确认。
- ❌ 表格全自绘。
- ❌ 设置项弹模态框。
- ❌ 状态面板轮询刷新（用事件总线）。
- ❌ 安全模块失效时"带病运行"。
- ❌ 开第二个 WebView2 实例。
- ❌ 对话区加"正在输入"动画（用静态状态 + 最后响应时间代替）。
- ❌ 托盘图标靠 GDI+ 缩放（提供多套 PNG）。
- ❌ 用 `DataGridView.VirtualMode` 承载可变行高展开内容（富内容进 HTML）。
- ❌ "降级到原生表格"却不设计原生表格（降级路径必须是实作，否则不写）。
- ❌ 在规格书里硬编码色值（统一走配色规划书 token，v1.3）。
- ❌ 给 `fallback` 行加整行底色（用小面积 [备用] 标签，v1.3）。

---

# 附录：提示词包

## 提示词 1：开发 UI 时的系统提示词（v1.4 同步更新）

```text
你是我 AhzScribe 项目的 UI 开发助手。这是一个 C# WinForms + Scintilla + WebView2
的单文件（partial class）桌面应用，正在从"工具型 Agent"升级为"硅基生物观察窗"。

硬约束（违反任何一条请拒绝实现）：
1. 对话不是主界面。主界面是"状态观察窗"，顶栏 + 左栏 + Tab 内容区。
2. 托盘是主要驻留形态，主窗口关闭 = 缩到托盘，不是退出。
3. 脉冲之间的界面必须"安静"：不跳动、不闪烁、不自动刷新。
4. 事实层（程序数据）用等宽+中性色；解释层（LLM 输出）用斜体+浅色背景。
5. none 行动在脉冲列表中必须可见，和 message 一样可展开。
6. 只在三种情况弹托盘气泡：主动消息、高风险待确认、急停。
7. 急停按钮单击即停，不弹确认框。
8. 颜色只用于语义，不用于装饰；所有色值必须引用《硅基生物 UI 配色规划书 v1.1》的 token，禁止硬编码色值。
9. 表格和富内容列表用 WebView2 HTML，不要求全自绘；不允许用 DataGridView 虚拟模式承载可变行高展开。
10. UI 更新通过 UIStateBus 事件驱动，不允许面板自己轮询文件。
11. WebView2 全局只有一个实例，脉冲、记忆、审计共用，切 Tab 只换 HTML 内容；切 Tab 前把滚动/过滤/展开状态写入 localStorage，切回时恢复。
12. 安全模块（审计/急停/权限判定）失效时，Agent 降级只读，不得带病运行。
13. 任何 UI 模块崩溃不影响 Agent 主循环。
14. 尺寸按 96 DPI 逻辑像素标注，渲染时按 Graphics.DpiX / 96.0 缩放。
15. 审计不可写时写备用通道 %LOCALAPPDATA%\Ahz\audit_fallback\audit_fallback.jsonl（fallback: true），恢复后合并回主审计；合并必须"先写后清"（全部写入成功才清空备用文件，中途失败整体保留重试），合并后按 audit_id 去重；降级路径必须是可实作的只读快照列表。
16. DPI 设置分栈实现：.NET 10 用 Application.SetHighDpiMode(PerMonitorV2)，.NET Framework 4.8 用 app.config 的 DpiAwareness=PerMonitorV2，用 #if 分区；Win10 1703 以下 4.8 分支退化 System DPI。
17. WebView2 连续 3 次启动恢复失败后进入永久降级模式（状态写 config.json 的 ui_state.html_disabled），停止自动重试，只在设置 Tab 提供手动重试；手动重试成功即退出。

技术栈：
- 主体 WinForms 自绘（顶栏、左栏、Tab 头、底栏）
- 脉冲/记忆/审计用本地 HTML + WebView2 单实例，JS 通过 host 对象调用 C#
- 深色主题，色值见《硅基生物 UI 配色规划书 v1.1》
- .NET 10 为主，兼容 .NET Framework 4.8 的代码用 #if 分区

输出要求：
- 纯 C# 代码（WinForms，不用 XAML）。
- 自绘给 OnPaint 重写和 GDI+ 绘制代码。
- WebView2 桥接给 AddHostObjectToScript 注册代码和 HTML 侧 JS 示例。
- 数据绑定引用 UIStateBus 事件，不直接读文件。
- 所有字段名来自规格书第 8 节 ui_data_contract.yaml，不自造。
- 所有颜色引用 Theme 静态类 token（对应配色规划书），不出现裸色值。

现在请为【具体组件名】生成实现代码。
```

## 提示词 2：UI 状态注入 Agent 上下文

```text
当前 UI 状态：
- 主窗口：{打开/最小化到托盘}
- 当前 Tab：{对话/脉冲/记忆/审计/任务/设置}
- 顶栏圆点：{休眠/脉冲中/待确认/急停}
- 左栏当前焦点：{current_focus}
- 未完成意图显示：{前 3 条 open_intents}
- 最近脉冲：{时间} {触发源} → {行动}
- 待确认审批：{数量} 条

约束：
- 如果主窗口最小化到托盘，你的主动消息会触发托盘气泡通知；
  请优先考虑对方是否可能正在专注其他事情。
- 如果当前 Tab 是"审计"或"记忆"，说明对方正在观察你的状态，
  此时若你选择 none，对方会看到"想到但未行动"的记录。
  请确认你的 reason 写得足够清楚。
- 如果待确认审批 > 0，可在回复中简短提及，但不要主动催促。
```

## 提示词 3：脉冲日志摘要生成（给本地小模型）（v1.3 修订）

```text
把下面这条脉冲记录压缩成一行折叠态摘要，不超过 60 个中文字符。

输入格式：
{trigger_type: ..., thought: ..., action: ..., reason: ...}

输出格式（严格一行，不要前后缀）：
{trigger_type} → {行动}（{thought 或 reason 的极简摘要}）

示例：
输入：{trigger_type: "body_event", thought: "空闲12分钟，该问文件确认了，但对方可能忙",
      action: "none", reason: "用户在忙，选择不打扰"}
输出：body_event → none（想到：该问文件确认，但选择不打扰）

规则：
- trigger_type 保持原样，不翻译。
- 行动用原值（message/tool/memory/wait/continue/none/finish）。
- 括号内用 thought 极简版，去掉"我"、去掉客套。
- thought 为空则用 reason 代替。
- 摘要含关键信息（触发原因 / 决定 / 结论），允许 40-60 字；
  折叠态显示时超长部分由 CSS text-overflow: ellipsis 截断，完整内容在展开态可见。
- 不解释、不评论、不加标点以外的装饰。
```

## 提示词 4：托盘气泡内容生成

```text
生成一条托盘气泡通知，触发类型：{主动消息/高风险待确认/急停}。

规则：
- 主动消息：显示消息正文前 50 字，不加工、不摘要、不改语气。
- 高风险待确认：格式「请求 {操作类型}：{对象}」，不超过 30 字，
  例如「请求删除：C:\Projects\old\src」。
- 急停：固定文案「已急停，所有脉冲停止」。

禁止：
- 不加"提示您""请注意"等客服腔前缀。
- 不加 emoji。
- 不加时间戳（托盘本身显示时间）。
```

---

# 附录：落地检查速查

```
U1 顶栏 + 托盘 + 首次引导 + 启动自检 + 备用通道
   → 验收：关窗驻留、图标变色、急停可触发、首次有引导、审计不可写有降级且备用通道有记录
U2 脉冲 Tab（HTML + localStorage 状态保留）+ 对话保底
   → 验收：能读出 24 小时生活节律；对话有状态和最后响应时间；切 Tab 状态不丢
U3 审计 Tab（WebView2 单实例 + 备用通道合并 + [备用] 标签）
   → 验收：工具调用链可查，blocked 有红线，备用条目带 [备用] 可区分
U4 左栏 + 底栏 + 故障兜底（含只读快照列表）
   → 验收：左栏随脉冲更新；WebView2 崩溃有兜底且三次后进只读列表
U5 记忆 Tab（共用 WebView2）
   → 验收：记忆可跳审计；disputed 行 bg-disputed 底 + 黄边框
U6 任务 + 设置 + 契约校验
   → 验收：活跃任务显示进度；契约版本不匹配拒绝启动
```

**一句话**：U1 + U2 跑通即可验收整套 UI 理念是否成立。先别管 U3-U6，把"托盘 + 顶栏 + 脉冲可读 + 对话不假死 + 坏的时候有地方可查"这五件事做实，你就能判断这个观察窗是否真的适合你。
