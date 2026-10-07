# Augram.Sync.Git

The git adapter for sync between machines (requirements F8, "Sync between machines"): implements Core's `ISyncRepository` port over the **installed `git`**. Core does the merging; this project only moves one file per machine through a private git repo.

**May reference:** `Augram.Core` only. No package references; the architecture test in `tests/Augram.Core.Tests/Architecture` checks this.

**Must never contain:** UI, credentials, a git library, or any process start outside `GitRunner`.

## Entry point

```csharp
ISyncRepository repository = new GitSyncRepository(
    folder: Path.Combine(configFolder, "sync", "repo"),
    gitExecutable: "git",          // or a full path; default "git" from PATH
    machineLabel: "Joel's work PC"); // only for the fallback commit identity; default: the computer name
```

Every member may block for seconds (network) and is called from the sync worker only. No member throws for an expected failure; it returns `SyncOperationResult.Failed(line)`.

| Member | What it does |
|---|---|
| `Prepare(url)` | Folder absent or empty → `git clone -- <url> <folder>` (an empty remote is fine). Folder already a clone → its `remote.origin.url` must be the same repository (scheme and host case-insensitive, user name, trailing `.git` and `/` ignored; local paths with either slash, case-insensitive on Windows), else refused. A non-empty folder that is not a clone → refused. An http(s) URL with a user part or any URL with a password → refused (the clone would store it in plain text). HEAD unborn (empty remote, or a remote whose default branch is not `main`) → HEAD set to `main` and `origin/main` taken if it exists. Ensures `machines/`. |
| `Pull()` | `git fetch --prune origin`; nothing more when the remote has no `main` yet; HEAD unborn → `git merge --ff-only origin/main`; else `git rebase --autostash origin/main`, so this machine's unpushed commit is replayed on top. A failed rebase is aborted (`git rebase --abort`) and reported. |
| `ReadMachineFiles()` | Every `machines/*.json` as file name without extension → text. Unreadable files are skipped; Core reports broken content. |
| `Publish(id, content, message)` | `id` must be ASCII letters, digits and `-` (at most 64, not a Windows device name such as `CON`): it becomes a file name. Writes `machines/<id>.json` UTF-8 without BOM with `\n` line endings; `git add -- <that path>`; `git diff --cached --quiet -- <path>` says unchanged → no commit (but a commit still waiting from an earlier failed push is pushed); else `git commit -m <message> -- <path>` (that file only); `git push origin HEAD:refs/heads/main` (the first push to an empty repo creates `main`). A push rejected because another machine pushed first → `Pull()` and push once more. **Never `--force`.** |

Why `fetch` + `rebase` rather than `git pull --rebase`: one network call instead of two, no dependence on upstream-tracking config, and the same result. `--autostash` keeps a stray edit in the clone (or a file written by a publish whose commit failed) from blocking every later pull.

## Process rules (security)

All in `GitRunner`, the only place that starts a process:

- Arguments go through `ProcessStartInfo.ArgumentList`, never a joined command string; `UseShellExecute = false`, no window; a URL is passed after `--` so it can never be read as an option.
- `GIT_TERMINAL_PROMPT=0` and `GIT_ASKPASS` removed: git never waits on a prompt nobody can see. The credential helper's own GUI (Git Credential Manager's sign-in window) still appears. Stdin is closed at once.
- `LC_ALL=C`: git's messages are English on every system language, so the error mapping below works.
- `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE` and the other repository-redirecting variables are removed, and `GIT_CEILING_DIRECTORIES` is the clone's parent: a command can only ever act on the clone (a missing `.git` fails, it never finds a parent repository).
- Timeouts: 30 s for network commands (`clone`, `fetch`, `push`), 10 s for the rest. A late process is killed with its whole process tree.
- Commits use the user's configured git identity. With no `user.name`/`user.email` configured, that one command gets `-c user.name="Augram (<machine label>)" -c user.email=augram@localhost` (commit, and rebase, which also writes commits). The user's git config is never written.

## Credentials

Augram never reads, stores or logs a token or a credentialed URL. Authentication is git's credential helper. Every line returned is scrubbed by `CredentialScrubber` (`https://user:token@host/…` → `https://host/…`), including lines git produced.

## Error lines (`GitFailure`)

| Cause (from git's stderr) | Line |
|---|---|
| git cannot be started | `git is not installed or not on PATH` |
| refused or missing credentials (`Authentication failed`, `terminal prompts disabled`, HTTP 401/403, `Permission denied (publickey)`) | `sign-in needed: GitHub refused the credentials; push once from a terminal in <folder> or sign in with Git Credential Manager` |
| `Repository not found`, `repository '…' does not exist`, `does not appear to be a git repository` | `the repository was not found or this account cannot see it` |
| DNS, connect, refused, unreachable, timed out | `offline or the host cannot be reached` |
| the call outlived its timeout | `git <command> did not finish in time and was stopped; try again later` |
| anything else | `git <command> failed: <git's last stderr line, hints skipped, fatal:/error: prefix removed>` |
| a failed rebase during pull | the line above plus ` (the pull was undone)` |

Folder problems Augram itself hits (no permission, a locked file) come back as `the sync folder could not be used: <reason>`.

## Folder layout

```
<config>/sync/repo/        the clone (Augram's own; nothing else writes here)
  .git/
  machines/
    <machine-id>.json      one per machine; each machine commits only its own
```

Branch `main` only. Each machine writes only its own file, so a rebase never conflicts and a rejected push is always resolved by pull-then-push.

## What Joel does once per machine

1. Create the private repo once (e.g. `augram-settings` on GitHub, empty or with a README) and paste its plain https URL into Options › Sync on each machine.
2. **Windows:** nothing but sign in when Git Credential Manager (installed with Git for Windows) opens its window on the first push or fetch. If the window does not appear, run `git push` once from a terminal in the clone folder.
3. **macOS:** git's Keychain helper (`credential.helper osxkeychain`, the default with Apple's and Homebrew's git) or `gh auth setup-git` once; then sign in when asked. The token lives in the Keychain, never in Augram.

## Tests

`tests/Augram.Sync.Git.Tests`: real git against bare repositories in temp folders, no network, git cut off from the developer's own config (`GIT_CONFIG_GLOBAL` to a temp file, `GIT_CONFIG_NOSYSTEM=1`).
