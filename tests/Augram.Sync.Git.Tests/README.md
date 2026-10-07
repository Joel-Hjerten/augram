# Augram.Sync.Git.Tests

xunit tests for `Augram.Sync.Git` with the **real installed git** (present on the Windows and macOS CI runners) and no network: every "remote" is a bare repository (`git init --bare`) in the test's own temp folder, and every machine is a `GitSyncRepository` clone beside it.

**May reference:** `Augram.Sync.Git` (and `Augram.Core` through it), xunit. `Augram.Sync.Git` exposes its internals to this assembly.

**Isolation:** `GitSandbox` gives each test a fresh temp root, deleted on dispose (read-only git objects included; a failed delete is tolerated). Git runs with `GIT_CONFIG_GLOBAL` pointing at a file in that root and `GIT_CONFIG_NOSYSTEM=1`, and the identity variables removed, so the developer's identity, hooks and settings never leak in and no real config is read or written. A test that needs a configured identity writes it into that file.

| File | Covers |
|---|---|
| `PrepareTests` | clone of an empty remote puts HEAD on `main`; repeat with another spelling of the URL; refuses a clone of another URL, a non-empty non-clone folder and a URL carrying a credential (without repeating it); a missing git executable and a nonexistent remote give their lines, not exceptions; Pull/Publish before Prepare fail cleanly |
| `PublishTests` | the first publish creates `main` with the file as written (`\n`, no BOM); a second machine clones (remote default branch `main` or `master`) and both read both files; unchanged content makes no commit; a push rejected because the other machine pushed first is pulled and retried; machines alternating without pulling stay in step; ids that are not plain file names are refused and nothing is written |
| `PullTests` | no branch yet → nothing to do; an unborn HEAD takes the remote files; an unpushed own commit (push failed while the remote was away) stays on top after a pull, and the next publish pushes it even with unchanged content; `ReadMachineFiles` skips what is not `*.json` |
| `CommitIdentityTests` | fallback identity only when none is configured, never written to config; a configured identity is used; a rebase after a rejected push also works with no identity; the fallback name drops `<`, `>` and control characters |
| `GitRunnerTests` | git's environment (`GIT_TERMINAL_PROMPT=0`, `LC_ALL=C`, ceiling set, no `GIT_ASKPASS`/`GIT_DIR`), a late call killed with its whole process tree, a missing executable reported |
| `GitFailureTests`, `CredentialScrubberTests`, `RemoteUrlTests`, `MachineFilesTests` | the error-line mapping, userinfo scrubbing, URL comparison and credential detection, machine id rules |
