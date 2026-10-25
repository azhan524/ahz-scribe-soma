# ahz-scribe-contracts

`brain` / `body` / `face` 之间唯一允许共享的纯数据契约。

## 内容

- `ActionRequest` / `ActionResult`
- `PermissionTier`
- `IActionExecutor`
- `IAuditSink`
- `BodyStatus` / `IBodyStatusSource`
- `PulseEvent` / `IPulseSink`
- `action-request.schema.json`

## 规则

- 不引用 WinForms、CefSharp、Shell、文件系统或模型 SDK。
- 只放数据与接口，不放实现。
- 任何字段变更都要同步 JSON Schema 和三个模块的兼容测试。
- 目标是 `net10.0`；旧单文件栈继续用 `local-agent` 里的同名类型，迁移完成后再统一。

## 构建

```bat
dotnet build contracts\AhzScribe.Contracts.csproj
```

当前结果：

```text
0 warnings / 0 errors
```
