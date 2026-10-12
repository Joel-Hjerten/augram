#!/usr/bin/env node
// PreToolUse hook (Bash|PowerShell) in .claude/settings.json: approves the
// cleanup of an agent worktree without a permission prompt.
//
//   git worktree remove .claude/worktrees/agent-<id>
//
// `Bash(git:*)` is already allowed, but Claude Code's own check on commands that
// delete under .claude/ still prompts; a hook's "allow" is what gets past it.
// Only that exact shape is approved: one worktree under .claude/worktrees/agent-*,
// relative or absolute under this checkout, optionally quoted, nothing chained.
// No --force: git then refuses a worktree with uncommitted changes, so an
// agent's unmerged work is never thrown away silently. Anything else -> no
// output, and the normal permission rules decide.

import fs from 'node:fs';

let input;
try {
  input = JSON.parse(fs.readFileSync(0, 'utf8'));
} catch {
  process.exit(0);
}

const command = (input?.tool_input?.command ?? '').trim();
const escape = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const sep = '[\\\\/]';
// The project dir may arrive with either slash style or drive-letter case.
const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input?.cwd ?? '';
const parts = projectDir.split(/[\\/]+/).filter(Boolean);
const root = parts.length ? `(?:/?${parts.map(escape).join(sep)}${sep})?` : '';
const pattern = new RegExp(
  `^git worktree remove (["']?)${root}\\.claude${sep}worktrees${sep}agent-[0-9a-z]+\\1$`,
  'i',
);

if (pattern.test(command)) {
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'allow',
      permissionDecisionReason: 'Agent worktree cleanup (scripts/allow-worktree-remove.mjs)',
    },
  }));
}
