#!/usr/bin/env node
// PreToolUse hook (Bash|PowerShell) in .claude/settings.json: approves the
// cleanup of an agent worktree without a permission prompt.
//
//   cd <this checkout> && git worktree remove .claude/worktrees/agent-<id>
//     && git branch -D <branch> worktree-agent-<id> [2>&1 | tail -2]
//
// `Bash(git:*)` is already allowed, but Claude Code's own check on commands that
// delete under .claude/ still prompts; a hook's "allow" gets past it. (It does
// not get past an ask rule, so `git branch -D` is guarded here instead of on the
// ask list - see the force-delete check below.) The command is split on `&&` and
// approved only if every part is one of:
//   - `cd` to this checkout
//   - `git worktree remove` of one .claude/worktrees/agent-* (no --force: git
//     then refuses a worktree with uncommitted changes)
//   - `git branch -d|-D` of branches (not main) that are all fully merged into
//     main, checked here, now - so no unmerged work can be deleted
// An optional `2>&1` and `| tail|head -N` at the end are fine. Anything else
// (another command, `;`, `||`, other pipes, $(), backticks) -> no output, and
// the normal permission rules decide.

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

let input;
try {
  input = JSON.parse(fs.readFileSync(0, 'utf8'));
} catch {
  process.exit(0);
}

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input?.cwd ?? '';
if (!projectDir) process.exit(0);

const escape = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const sep = '[\\\\/]';
// The project dir may arrive with either slash style or drive-letter case.
const parts = projectDir.split(/[\\/]+/).filter(Boolean);
const rootPath = `/?${parts.map(escape).join(sep)}`;
const quoted = (body) => `(["']?)${body}\\1`;

const cdRe = new RegExp(`^cd ${quoted(`${rootPath}${sep}?`)}$`, 'i');
const removeRe = new RegExp(
  `^git worktree remove ${quoted(`(?:${rootPath}${sep})?\\.claude${sep}worktrees${sep}agent-[0-9a-z]+`)}$`,
  'i',
);
const branchRe = /^git branch (?:-d|-D|--delete(?: --force)?)((?: [\w./-]+)+)$/;

const git = (...args) =>
  execFileSync('git', args, { cwd: projectDir, stdio: ['ignore', 'pipe', 'ignore'], timeout: 5000 });

function mergedIntoMain(branch) {
  if (branch.startsWith('-') || branch === 'main') return false;
  try {
    git('rev-parse', '--verify', '--quiet', `refs/heads/${branch}`);
    git('merge-base', '--is-ancestor', `refs/heads/${branch}`, 'refs/heads/main');
    return true;
  } catch {
    return false;
  }
}

function approved(segment) {
  if (cdRe.test(segment) || removeRe.test(segment)) return true;
  const m = branchRe.exec(segment);
  return !!m && m[1].trim().split(/ +/).every(mergedIntoMain);
}

const decide = (permissionDecision, reason) => {
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision, permissionDecisionReason: reason },
  }));
  process.exit(0);
};

// A force delete anywhere in the command (-D, --delete --force, -df) asks
// unless every branch it names is merged into main. This replaces the
// `Bash(git branch -D:*)` ask rule: an ask rule beats a hook's "allow", so
// with it every cleanup prompted even when the branches were merged.
const raw = input?.tool_input?.command ?? '';
const forceDeleteRe = /git branch (?:-D|-df|-fd|--delete --force|--force --delete)\b([^;&|\n]*)/g;
for (const m of raw.matchAll(forceDeleteRe)) {
  const names = m[1].replace(/\s+\d?>&?\d?\S*.*$/, '').trim().split(/\s+/).filter(Boolean);
  if (!names.length || !names.every((n) => /^[\w./-]+$/.test(n) && mergedIntoMain(n))) {
    decide('ask', 'git branch -D of a branch not merged into main');
  }
}

const command = raw
  .trim()
  .replace(/(?: 2>&1)?(?: \| (?:tail|head) -n? ?\d+)?$/, '');
if (/[;|`$<>\n]|&(?!&)|(?<!&)&/.test(command.replace(/ && /g, ' '))) process.exit(0);

const segments = command.split(' && ').map((s) => s.trim());
if (segments.some((s) => removeRe.test(s) || branchRe.test(s)) && segments.every(approved)) {
  decide('allow', 'Agent worktree cleanup (scripts/allow-worktree-remove.mjs)');
}
