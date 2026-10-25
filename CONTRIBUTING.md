# Contributing

## Before you start

This repository is in a migration period. The current executable code lives in `../local-agent`; the `ahz-scribe-*` directories are the target layout.

Please open an issue before large refactors, especially anything touching:

- permission / approval logic;
- audit broker or ACLs;
- shell, file, network and desktop tools;
- prompt construction and memory injection;
- licensing.

## Development rules

- Windows-first; .NET Framework 4.8 / C# 5 is the compatibility baseline.
- Do not add string interpolation, `?.`, `nameof`, expression-bodied members, or other C# 6+ syntax to the legacy source.
- Keep dangerous tools approval-gated and audit-logged.
- Do not commit `config.json`, `sessions.json`, API keys, QQ credentials, audit keys, or runtime data.
- Do not weaken read-only/workspace-write guards for convenience.
- Add or update a regression test under `devtools/bench/` for behavior changes.
- Keep names lowercase kebab-case; `ahz-scribe` is always the first identifier.

## Local checks

```bat
cd ..\local-agent
build.cmd
devtools\bench\run-ui-tests.cmd
```

Expected baseline: no failures. Test counts change over time; use the script output, not a hard-coded number.

## Pull requests

- One focused change per PR.
- Explain the user-visible behavior and the security impact.
- Include test evidence.
- For UI changes, include a screenshot and note DPI/scale.
- For audit changes, state whether HMAC/broker behavior changed.

## License

By contributing, you agree that your contribution is licensed under AGPL-3.0 unless explicitly stated otherwise.
