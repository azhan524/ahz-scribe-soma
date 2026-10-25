# Open-source decision

**Decision (2026-10-26): AGPL-3.0.** The project stays copyleft; closed hosted forks are not permitted. Revisit only before the first public release.

## Recommendation

**Open source, but not as a raw dump of the current single-file agent.**

Publish when the repository is self-contained, the three-module split is real enough to audit, and the security boundaries are documented and tested.

## Why open source

- The project is a substantial local-agent implementation; public review improves security.
- A public repository is a better portfolio than screenshots.
- Contributors can add tool adapters, UIA tests and provider support.
- The audit/broker design is worth sharing.

## Why not now

- The code is ~24k lines in one file; review and contribution cost is high.
- `brain/body/face` is still a directory contract, not a process boundary.
- The public clone currently depends on `../local-agent`, which is not public.
- .NET 10 build is not reproducible here.
- A desktop agent with shell/UIA access needs a threat model and security policy before attention.
- Old development docs contain personal paths and test account IDs.

Open sourcing a security-sensitive agent before those are fixed creates a false impression of safety and invites low-quality fork/abuse.

## License

### AGPL-3.0 (current draft)

- keeps modifications open, including network use;
- fits a project that may be run as a service;
- may deter companies and some contributors.

### Apache-2.0

- maximum adoption and commercial friendliness;
- explicit patent grant;
- allows closed-source derivatives.

### Recommendation

- If the goal is **community / keeping the agent open**: AGPL-3.0.
- If the goal is **adoption / portfolio / commercial ecosystem**: Apache-2.0.
- Do not use a custom “source-available” license and call it open source.

The license file is currently AGPL-3.0; it can be changed before the first public release, but not casually after contributors join.

## Release gates

- [ ] self-contained build from a clean clone;
- [ ] three modules build;
- [ ] .NET 10 build in CI;
- [ ] full test suite green;
- [ ] no secrets/personal paths;
- [ ] SECURITY + threat model;
- [ ] audit install/uninstall tested on a clean VM;
- [ ] release notes list limitations.

## What to publish first if impatient

A **documentation + demo** release is acceptable:

- README, architecture, threat model, screenshots;
- signed binary release;
- no source until the split is ready.

But do not call it “open source” until the source is actually published under an OSI license.
