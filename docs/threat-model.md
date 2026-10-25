# Threat Model

## Assets

- user files and workspace;
- API keys and provider credentials;
- QQ account / NapCat token;
- audit log and HMAC key;
- memory and session history;
- the user's desktop session.

## Actors

- user;
- model provider;
- malicious prompt / web page / file;
- local unprivileged process;
- malware already running as the same user;
- remote QQ sender (if bridge enabled).

## Trust boundaries

1. **OS user** — the real boundary. A process running as the same user can generally read the user's files and manipulate the desktop.
2. **Audit broker** — separate LocalSystem process; normal user cannot read the HMAC key or rewrite the main audit file.
3. **Tool permission** — read-only / workspace-write / full; approval gate for dangerous tools.
4. **Model boundary** — model output is untrusted input; tool arguments are validated by body.

## Threats and mitigations

| Threat | Mitigation | Residual risk |
|---|---|---|
| Prompt injection asks for destructive shell | approval + read-only/workspace guard + audit | user may approve in full mode |
| Path traversal outside workspace | path normalization + outside approval | same-user shell can bypass if full access |
| Audit tampering | append-only ACL + HMAC chain + broker; payloads user-read-only | broker accepts false appends; payloads are immutable but not a single stream |
| Key theft | key ACL SYSTEM/Administrators; broker only | admin/malware can access |
| Malicious desktop UI | UIA first; click/type approval | coordinate clicks can hit wrong control |
| Memory poisoning | source audit id, disputed track, review | model can still be influenced |
| QQ remote control | owner allowlist, `/ahz` prefix | QQ account compromise |
| Model data leak | local-first, provider choice | cloud provider receives prompt |

## Security invariants

- `完全访问` is never the default.
- Read-only and plan modes block write tools and desktop input.
- Every tool call carries an audit entry; audit failure is visible.
- Audit main file is append-only for normal users.
- No secrets in repository.
- Pulse decisions cannot execute tools/memory; only `none/message/wait`.

## Known gaps

- payloads are user-read-only and broker-written, but not a single append-only stream;
- false appends by the app are not prevented (only alteration/deletion);
- no separate low-privilege account for the broker (LocalSystem is powerful);
- no reproducible-build verification;
- no independent third-party security audit.

## Reporting

See [SECURITY.md](../SECURITY.md). Do not publish exploit details before a fix is available.
