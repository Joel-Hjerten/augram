# Augram.Import.StrokesPlus

Importer for StrokesPlus.net's live JSON (`%APPDATA%\StrokesPlus.net\StrokesPlus.net.json`): gestures (M1 step 10), then apps, actions, ignored apps and settings per the mapping table in `docs/plans/0001-first-version.md` §C1 (M2 step 8). Produces Core records plus an import report of what was skipped and why.

**May reference:** `Augram.Core` only. `System.Text.Json` is BCL and fine.

**Must never contain:** UI, OS calls, SharpHook, or any package reference. The architecture test in `tests/Augram.Core.Tests/Architecture` checks this.

Empty for M0.
