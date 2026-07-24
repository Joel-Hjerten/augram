# docs/ — how documentation works in this repo

Agent-for-agent documentation, modeled on the Chimera repo's system with a few refinements. The goal: a future agent (or Joel) can pick up any task without reimplementing existing solutions or relitigating closed decisions.

## Taxonomy

| Folder / file | What goes there | Lifecycle |
|---|---|---|
| [requirements.md](requirements.md) | The living v1 requirements, each item tagged DECIDED / LEANING / OPEN | Updated as decisions land; the open-decisions index at the bottom is the planning dashboard |
| [decisions/](decisions/) | ADRs (`NNNN-title.md`) — one per significant decision, with status PROPOSED / ACCEPTED / SUPERSEDED. **Closed ADRs are not relitigated** unless Joel reopens them. | Append-only; supersede rather than edit history |
| `plans/` | Active implementation plans for features being built | Move to `plans/completed/` when shipped — completed plans are the historical record of *why the code is shaped this way* |
| `reference/` | Durable how-it-works docs for subsystems (the things an agent must read before editing an area) | Kept current with the code |
| `learnings/` | Hard-won debugging insight and "we tried X, it fails because Y" write-ups | Append; these prevent repeat burns |
| [handoff.md](handoff.md) | The original research handoff (prior art, recognition algorithm, platform notes) | Frozen historical input; requirements.md supersedes it where they conflict |

## Conventions (inherited from Chimera, adjusted)

1. **CLAUDE.md stays lean and links out.** It holds ground rules and a read-first table; content lives here in docs/. (Chimera's CLAUDE.md grew large; we start disciplined.)
2. **Read-first table.** When a subsystem gains invariants, it gets a reference/ or learnings/ doc and a row in CLAUDE.md's table. Editing without reading the row's doc is how repeat bugs happen.
3. **ADRs are the anti-relitigation mechanism** — Chimera encodes closed decisions as CLAUDE.md bullets ("do NOT reintroduce X"); we give each its own numbered file so it can be linked, superseded, and found. New here, recommended back-port to Chimera someday.
4. **Load-bearing comments in code** state invariants where an agent would otherwise "fix" them, and link the relevant doc. Comments say *why*, docs say *how it fits together*.
5. **Don't reimplement — extend.** Before writing anything, check requirements.md, the read-first table, and reference/. If a pattern exists, add to it; if a pattern is missing, add the pattern *and its doc* in the same change.
