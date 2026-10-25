# ahz-scribe-brain-host

独立 brain 进程 v0。

```text
src/Program.cs
```

启动：

```powershell
$env:DEEPSEEK_API_KEY = "..."
$env:AHZ_MODEL_BASE = "https://api.deepseek.com"
$env:AHZ_MODEL_NAME = "deepseek-v4-pro"
$env:AHZ_BRAIN_PIPE = "ahz-scribe-brain"
.\AhzScribe.BrainHost.exe
```

face 侧设置同名：

```powershell
$env:AHZ_BRAIN_PIPE = "ahz-scribe-brain"
.\AhzScribe.Face.exe
```

已验证：

```text
/ask → [brain-host echo] ...
```

已验证独立 host 的 `/tool` 和审批：
```text
HOST-PIPE-TOOL-OK
批准 run_shell ? [y/N] → HOST-PIPE-APPROVAL-OK
```
