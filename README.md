# ahz-scribe

[![repo-hygiene](https://github.com/azhan524/ahz-scribe-soma/actions/workflows/repo-hygiene.yml/badge.svg)](https://github.com/azhan524/ahz-scribe-soma/actions/workflows/repo-hygiene.yml)
[![build](https://github.com/azhan524/ahz-scribe-soma/actions/workflows/build.yml/badge.svg)](https://github.com/azhan524/ahz-scribe-soma/actions/workflows/build.yml)

> **Pre-release**：brain / body / face 已拆成可构建模块，但最终 UI 与部分工具仍在迁移中。
>
> 这是一个能读写文件、执行 Shell、操作桌面的本地 Agent。**不要在生产机、共享账户或存有敏感数据的机器上直接授予完全访问权限。**

## 它是什么

Ahz Scribe 是一个本地优先的桌面 Agent：

- 对话、调用工具、读写文件、运行命令；
- 有持久会话、自动 episode、双轨记忆；
- 工具动作写入可校验审计；
- 桌面操作有审批，坐标点击只作兜底；
- 模型和 body 通过命名管道隔离。

## 架构

```text
ahz-scribe-face      交互：窗口 / CLI / 审批 / 展示
        │
ahz-scribe-brain     思考：模型 / 规划 / 记忆 / 工具循环
        │
ahz-scribe-body      执行：文件 / Shell / 网络 / 桌面 / 截图
        │
        └── AhzAuditBroker（可选 Windows 服务，HMAC）
```

边界：

- brain 不直接碰工作区、Shell、进程；
- body 是唯一执行工具的进程；
- face 只调 BrainHost，不直接持有 BodyClient；
- 危险动作必须由 face 审批；
- 审计只追加，不覆盖。

详见 [docs/architecture.md](docs/architecture.md) 与 [docs/threat-model.md](docs/threat-model.md)。

## 当前状态

已验证：

```text
S0 状态 / 会话
S1 审计（ACL + broker + HMAC）
S2 记忆（semantic / episodic / disputed）
S3 身体状态 + workspace_dirty
S4 脉冲候选 + 每日内驱
S5 主动消息内容约束
S6 桌面工具 + 审批
```

拆分为可构建模块：

```text
contracts              共享契约
ahz-scribe-body        body v0（文件 / shell / 搜索 / 网络 / 桌面 / 截图）
ahz-scribe-brain       MemoryStore / PulseScheduler / AgentRunner / ToolAgentRunner
ahz-scribe-brain-host  独立 brain 进程
ahz-scribe-face        console + WinForms v0（对话 / 轨迹 / 记忆）
```

已知限制：

- 旧单文件版本的完整 Codex 风格 UI 尚未迁入；
- body 工具表未完全对齐旧版；
- 桌面的 DPI / 多显示器校正未完成；
- `rollback_self` 的审计反向链接未完整验证；
- Linux / macOS 不支持 body 的桌面工具。

## 构建

需要 .NET 10 SDK。

```powershell
dotnet build contracts/AhzScribe.Contracts.csproj
dotnet build ahz-scribe-body/src/AhzScribe.Body.csproj
dotnet build ahz-scribe-brain/src/AhzScribe.Brain.csproj
dotnet build ahz-scribe-brain-host/src/AhzScribe.BrainHost.csproj
dotnet build ahz-scribe-face/src/AhzScribe.Face.csproj
```

## 运行

### 1. body

```powershell
$env:AHZ_BODY_DATA = "<repo>/_body-data"
$env:AHZ_BODY_PIPE = "ahz-scribe-body"

& "<repo>/ahz-scribe-body/src/bin/Debug/net10.0-windows/AhzScribe.Body.exe"
```

看到：

```text
ahz-scribe-body: pipe=ahz-scribe-body
```

保持窗口。

### 2. face

另开 PowerShell：

```powershell
$env:AHZ_BODY_PIPE = "ahz-scribe-body"
$env:DEEPSEEK_API_KEY = "<your key>"
$env:AHZ_MODEL_BASE = "https://api.deepseek.com"
$env:AHZ_MODEL_NAME = "deepseek-v4-pro"

& "<repo>/ahz-scribe-face/src/bin/Debug/net10.0-windows/AhzScribe.Face.exe" --ui
```

不启动 body 也可以 `/ask`；`/tool` 需要 body。

## 验证

```powershell
# body
& "<body-exe>" --self-test
& "<body-exe>" --ipc-test

# brain
dotnet run --project ahz-scribe-brain/tests/AhzScribe.Brain.CoreSmoke.csproj

# face
& "<face-exe>" --session-test
& "<face-exe>" --memory-real   # 需要 DEEPSEEK_API_KEY
```

## 审计服务

审计 broker 是可选的 Windows 服务；安装脚本在旧版 `local-agent/devtools`，本仓库不包含服务二进制。

没有 broker 时 body 使用本地 JSONL 审计作为回退。

## 安全

- 默认不要给 `Full` 权限；
- API Key 只放环境变量，不写进仓库；
- 不提交 `config.json`、`sessions.json`、`audit/`、`*.log`、`_face-data/`；
- 发现安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。

## License

GNU Affero General Public License v3.0，见 [LICENSE](LICENSE)。

第三方依赖（CefSharp、Scintilla.NET 等）保留各自许可证；见 [NOTICE](NOTICE)。
