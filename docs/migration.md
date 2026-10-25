# Migration Plan

## Principle

Do not create three empty folders and call it a split. The split must reduce authority:

- `brain` cannot open files, spawn processes, or touch the desktop.
- `body` owns all IO and approval.
- `face` owns rendering and user input.

The current single file is the source of truth until each boundary is extracted and tested.

## Current status

```text
contracts:  extracted and buildable (net10.0, 0 warnings / 0 errors)
brain:      directory only
body:       directory only
face:       directory only
legacy:     local-agent/src/AhzScribe.cs
```

`contracts/AhzScribe.Contracts.cs` now defines:

- `ActionRequest` / `ActionResult`
- `PermissionTier`
- `IActionExecutor`
- `IAuditSink`
- `BodyStatus` / `IBodyStatusSource`
- `PulseEvent` / `IPulseSink`

These are not wired into `local-agent` yet. The next step is to make the body implement
`IActionExecutor` and `IAuditSink` behind a local IPC boundary, then make brain call it.

## Source map

| Source group | Current location | Target | Notes |
|---|---|---|---|
| JSON / text / HTTP helpers | `Json`, `Txt`, `HttpJson` | shared contracts | small, stable |
| Tool registry / handlers | `Tools` | `ahz-scribe-body` | largest extraction |
| Snapshots / diffs / changes | `SnapStore`, `Diff`, `Changes` | `body` | file IO |
| Plugins | `Plugins`, `Plugin*` | `body` | loads code; high risk |
| Browser tools | `BrowserTools`, `AhzBrowser` | `body` + `face` | split automation vs UI |
| Audit | `Audit`, `AuditBrokerClient`, `AuditCli` | `body` | broker stays separate |
| Runtime state / intents | `RuntimeState`, `IntentStore` | `brain` | no IO except state file |
| Memory | `MemoryStore` | `brain` | JSONL read/write via storage interface |
| Body sensor | `BodySensor` | `body` | Windows APIs |
| Pulse | `PulseScheduler`, `PulseDecision`, `ProactivePolicy` | `brain` | uses body status/audit interfaces |
| Model clients | `Ollama*`, `OpenAi*`, `LlmFactory` | `brain` | network only |
| Config / providers | `AgentConfig`, `Provider` | shared config | keys via DPAPI |
| Agent loop | `AgentRunner` | `brain` | calls body via `IActionExecutor` |
| Approval UI | `ApprovalForm`, `Clarify*` | `face` | UI |
| Main UI | `MainForm`, `TranscriptView`, `MsgBlock`, controls | `face` | UI |
| CLI | `CliProgram`, `Program` | `face` | entry point |
| Audit broker | `AuditBrokerCore`, `AuditBrokerService` | `body` / service | separate executable |

## Extraction order

### Phase 1 — Contracts

- [x] `ActionRequest`, `ActionResult`, `PermissionTier`.
- [x] `IActionExecutor`, `IAuditSink`.
- [x] `BodyStatus`, `IPulseSink`.
- [x] Contracts project builds.
- [ ] Replace legacy duplicate types with the contracts assembly.

### Phase 2 — Body

- [ ] Move `Tools`, `SnapStore`, `Diff`, `Changes`, `Audit*`, `BodySensor`, `Plugins`, `Jobs`.
- [ ] Keep the existing approval callback interface; do not let body call WinForms directly.
- [ ] Build `ahz-scribe-body` as a console/library plus local IPC.
- [ ] Verify every tool still writes audit.

### Phase 3 — Brain

- [x] `BodyClient` IPC smoke passes (brain → body).
- [x] Brain v0: MemoryStore + PulseScheduler + AgentRunner + ToolAgentRunner real body loop.
- [ ] Move model clients, state, intents, full pulse.
- [ ] Replace direct `Audit.Record` calls with `IAuditSink`.
- [ ] Replace direct `Tools.*` calls with `IActionExecutor`.
- [x] DriveEngine four candidate types + deterministic score + daily cap (legacy pulse tests).
- [ ] Run the S4/S5 pulse tests against fake body/audit.

### Phase 4 — Face

- [ ] Move WinForms/CLI.
- [ ] Connect to brain/body via local IPC or in-process adapters.
- [ ] Keep approval UI in face; never bypass body.
- [ ] Re-run UI tests.

### Phase 5 — Process boundary

- [ ] Body as a separate process/service.
- [ ] Brain only has a pipe/HTTP client.
- [ ] Face only talks to brain/body APIs.
- [ ] Test kill/restart and audit continuity.

## Compatibility strategy

- Keep the legacy single-file build green during migration.
- Add new projects beside it; do not delete the old path until parity tests pass.
- Each phase must have a smoke test and a rollback point.
- No behavior changes bundled with a move-only commit.

## Definition of done

- [ ] Three buildable projects.
- [ ] Brain has no file/shell/desktop API.
- [ ] Body executes every tool and writes audit.
- [ ] Face has no direct tool calls.
- [ ] Process-boundary test passes.
- [ ] Legacy and new test suites both pass.
- [ ] .NET 10 build passes.
