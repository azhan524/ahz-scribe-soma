# Architecture

## Target layout

```text
ahz-scribe-face
  WinForms / CLI / approval UI / presentation
        │  ActionRequest / ActionResult / message sink
ahz-scribe-brain
  model calls / planning / persona / memory / pulse decision
        │  ActionRequest
ahz-scribe-body
  tools / files / shell / network / snapshots / body sensor / audit
        │
        ├── %LOCALAPPDATA%\AhzScribe  state & memory
        └── %PROGRAMDATA%\AhzScribe\audit
```

## Current layout

The repository now has real, buildable modules:

```text
contracts/                 ActionRequest / ActionResult / body status / memory / pulse contracts
ahz-scribe-body/src/       BodyServer, tool handlers, JSONL audit fallback
ahz-scribe-brain/src/      BodyClient, BrainHost, MemoryStore, PulseScheduler,
                           AgentRunner, ToolAgentRunner, OpenAiToolModel
ahz-scribe-brain-host/src/ brain over a named pipe (ask / tool / approval)
ahz-scribe-face/src/       console + simple WinForms (conversation / trace / memory),
                           session JSONL, automatic episode, memory panel
```

Boundaries:

- face calls `BrainHost` (in-process) or `BrainPipeClient` (independent host);
- brain owns the model, memory, pulse decision and tool loop;
- body is the only module that touches files, shell, network, desktop or screenshots;
- body is reached over the `ahz-scribe-body` named pipe;
- approval is a face callback; body never approves itself.

Not migrated yet:

- the old single-file Codex-style UI at `../local-agent`;
- full body tool parity with that legacy app;
- the complete `propose_upgrade -> build -> test -> restart -> rollback` lifecycle
  inside the new face.

Migration history and evidence are in [migration.md](migration.md) and
[engineering-2026-10-25.md](engineering-2026-10-25.md).

## Audit trust boundary

```text
AhzScribe.exe (normal user)
   │ named pipe
   ▼
AhzAuditBroker.exe (LocalSystem service)
   │ HMAC key: SYSTEM + Administrators only
   ▼
C:\ProgramData\AhzScribe\audit\audit.jsonl
   Users: Read + Append
```

Properties:

- main audit file cannot be overwritten or deleted by a normal user process;
- each entry has `prev_hash`, `entry_hash`, `hmac`;
- broker verifies HMAC and chain;
- `ahzscribe-cli.exe --audit verify` asks broker first;
- if broker is down, local verification reports “仅本地哈希”.

Non-goals / limits:

- broker accepts appends from the local user via the pipe; it prevents alteration, deletion and key theft, not false addition;
- payloads are not yet append-only;
- the model provider and QQ bridge are outside this trust boundary.

## Data flow

1. User input enters `face`.
2. `brain` builds messages and asks the model.
3. Tool calls become `ActionRequest`s.
4. `body` checks permission / approval, executes, records audit.
5. Results return to `brain`; `face` renders.
6. `PulseScheduler` can enqueue `intent_due` / `body_event` when no active task.

## Failure modes

- Broker unavailable: audit degrades to local ACL append (no HMAC); tools still work.
- Key unavailable: broker cannot sign; audit fails closed for broker writes.
- Model unavailable: pulse decision logs `model_error` and records `none`.
- Active task: all autonomous pulses defer.
- Crash: state file and audit are append-oriented; active task lease expires.
