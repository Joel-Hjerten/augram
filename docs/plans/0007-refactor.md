# Plan 0007: Refactor: one implementation per job, a map for every folder

**Status: STARTED 2026-10-11. Features are paused until this plan is done** (Joel, 2026-10-11). Bug fixes Joel asks for still go in; new features wait. The Mac catch-up for plans 0005 and 0006 waits too.

## Why

Joel, 2026-10-11: compartmentalise everything as it should be, and avoid the same functionality implemented more than once: one implementation used everywhere. A measurement the same day showed a second reason: agents spent a large part of every task relearning the area (5 of 12 minutes on a one-line UI change), and App, the biggest project (34k lines, 382 files), had one README while Core had 27.

## Decisions (Joel, 2026-10-11)

1. **Features pause** while the refactor runs, so slices never collide with feature work.
2. **The Wireframe theme is retired** ([ADR-0002](../adr/0002-code-and-repo-structure.md) §5b, amended). It was Default's base layer: what Default still uses moves into `Themes/Default/`, what Default overrides is deleted. UiShots renders every screen and gallery page before and after; they must match.
3. **Every App folder gets a README** (ADR-0002 §5, amended), written by the agent that reorganises that folder.
4. **Behaviour stays exactly as it is.** A slice that would change what Joel sees or what a gesture does is not a refactor; it goes back to Joel as a question.
5. **Models.** The inventory runs on Fable 5.1 at `high` (finding the same job written two different ways is its strength). The slices run on Opus 5.5 at the effort CLAUDE.md sets per area. Risky slices (Engine, capture, recognition, hold remaps, sync) get a separate Fable review before they merge. For merging the inventory into one list, the lead asks Joel to switch the session to Fable and back.

## Steps

1. **Wireframe folded into Default** (one agent; screenshot comparison). Handoff doc cut to the current state (lead).
2. **Inventory, read only** (Fable agents, one per area): Core model and steps; Core capture, recognition, hold remaps, sync, transfer; Engine and the Platform projects; importer, Sync.Git and App's sync/transfer/import; App Components; App view models and screens; App hosting, tray, overlay and tools; then Themes, after step 1. Each reports a map of its area and up to 25 findings: the same job in two places, types doing two jobs, ADR-0002 breaks, dead code, map gaps. Each finding has a recommendation, size and risk.
3. **One list** (lead, on Fable): the findings merged, deduplicated across areas, grouped into slices, each with a recommendation. Joel picks.
4. **Slices** (Opus agents, one per slice, in parallel where they don't share files): fold, split or move; tests green after each slice; the folder's README written or updated in the same commit.
5. **Close-out:** read-first table in CLAUDE.md updated, this plan moved to `completed/`, and the agent timing measurement repeated against the 2026-10-11 baseline (median agent run, share of time spent thinking).

## The list

(Filled in at step 3.)
