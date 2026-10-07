#!/usr/bin/env node
// Keep the shared Claude memory store + this checkout in sync across machines
// (Windows PC <-> Mac). Wired as Claude Code hooks in .claude/settings.json:
//
//   node scripts/memory-sync.mjs start   (SessionStart)
//     - memory repo: `git pull --rebase --autostash` (aborts cleanly on conflict)
//     - this checkout: `git fetch`, then REPORT ahead/behind vs upstream.
//       Never pulls code automatically - the agent sees the report and offers.
//
//   node scripts/memory-sync.mjs stop    (Stop - end of every agent turn)
//     - memory repo: if dirty -> commit; if ahead of origin -> pull --rebase + push.
//       Clean + not ahead (the common case) = two local git calls, no network.
//
//   node scripts/memory-sync.mjs prompt  (UserPromptSubmit - every user message)
//     - An open chat never re-runs SessionStart, so a switch to the other machine
//       and back left it stale. This mode does nothing unless the user's previous
//       message in this chat was more than MEMORY_SYNC_GAP_MINUTES (default 10)
//       ago; after such a break it pulls the memory repo and fetches this checkout
//       (6 s cap each), and only when something changed adds a note to the message:
//       the changed memory files and MEMORY.md lines, and the commits the other
//       machine pushed. Never pulls code itself. No change = no output, no tokens.
//       The per-chat timestamp lives in <tmp>/claude-memory-sync/.
//
// Memory dir = `autoMemoryDirectory` from .claude/settings.local.json
// (override: MEMORY_SYNC_DIR env var). Failures never block the session: they
// surface as a one-line systemMessage and the next Stop retries.

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const PROJECT_DIR = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const HOST = os.hostname();

function resolveMemoryDir() {
  if (process.env.MEMORY_SYNC_DIR) return process.env.MEMORY_SYNC_DIR;
  try {
    const s = JSON.parse(fs.readFileSync(path.join(PROJECT_DIR, '.claude', 'settings.local.json'), 'utf8'));
    const p = s.autoMemoryDirectory;
    if (p) return p.startsWith('~') ? path.join(os.homedir(), p.slice(1)) : p;
  } catch {}
  return null;
}
const MEMORY_DIR = resolveMemoryDir();

function git(cwd, args, { timeout = 20000 } = {}) {
  return execFileSync('git', ['-C', cwd, ...args], {
    encoding: 'utf8', timeout, stdio: ['ignore', 'pipe', 'pipe'],
  }).trim();
}

function tryGit(cwd, args, opts) {
  try { return { ok: true, out: git(cwd, args, opts) }; }
  catch (e) { return { ok: false, out: String(e.stderr || e.message).trim().split('\n').pop() }; }
}

function isRepo(dir) {
  return !!dir && fs.existsSync(dir) && tryGit(dir, ['rev-parse', '--is-inside-work-tree']).out === 'true';
}

function aheadBehind(cwd) {
  const r = tryGit(cwd, ['rev-list', '--left-right', '--count', 'HEAD...@{u}']);
  if (!r.ok) return null;
  const [ahead, behind] = r.out.split(/\s+/).map(Number);
  return { ahead, behind };
}

function emit(event, { systemMessage, context } = {}) {
  const out = {};
  if (systemMessage) out.systemMessage = systemMessage;
  if (context) out.hookSpecificOutput = { hookEventName: event, additionalContext: context };
  if (Object.keys(out).length) process.stdout.write(JSON.stringify(out));
}

const GAP_MS = Number(process.env.MEMORY_SYNC_GAP_MINUTES ?? 10) * 60_000;
const NETWORK_TIMEOUT_MS = 6000;

/** The JSON Claude Code sends a hook on stdin (session_id, prompt, ...); empty when run by hand. */
function readHookInput() {
  if (process.stdin.isTTY) return {};
  try { return JSON.parse(fs.readFileSync(0, 'utf8') || '{}'); } catch { return {}; }
}

/** Remembers when this chat last heard from the user; returns the previous time (0 = never). */
function touchSession(sessionId) {
  const dir = path.join(os.tmpdir(), 'claude-memory-sync');
  const safe = String(sessionId || 'no-session').replace(/[^A-Za-z0-9_-]/g, '_');
  const file = path.join(dir, `${path.basename(PROJECT_DIR)}-${safe}.json`);
  let previous = 0;
  try { previous = JSON.parse(fs.readFileSync(file, 'utf8')).lastPromptAt || 0; } catch {}
  try {
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(file, JSON.stringify({ lastPromptAt: Date.now() }));
  } catch {}
  return previous;
}

/** Lines added to or removed from MEMORY.md between two commits, without the diff headers. */
function indexChanges(before, after) {
  return tryGit(MEMORY_DIR, ['diff', '--unified=0', before, after, '--', 'MEMORY.md']).out
    .split('\n')
    .filter(line => (line.startsWith('+') || line.startsWith('-')) && !line.startsWith('+++') && !line.startsWith('---'))
    .map(line => `MEMORY.md ${line.startsWith('+') ? 'added' : 'removed'}: ${line.slice(1).trim()}`);
}

function prompt() {
  const previous = touchSession(readHookInput().session_id);
  // First message of the chat: SessionStart has just synced. A short gap: same machine, nothing to do.
  if (!previous || Date.now() - previous < GAP_MS) return;

  const notes = [];
  const shout = [];

  if (isRepo(MEMORY_DIR)) {
    const before = tryGit(MEMORY_DIR, ['rev-parse', 'HEAD']).out;
    const pull = tryGit(MEMORY_DIR, ['pull', '--rebase', '--autostash', '--quiet'], { timeout: NETWORK_TIMEOUT_MS });
    if (!pull.ok) {
      tryGit(MEMORY_DIR, ['rebase', '--abort']);
      shout.push(`memory sync: pull failed (${pull.out})`);
    } else {
      const after = tryGit(MEMORY_DIR, ['rev-parse', 'HEAD']).out;
      if (before && after && before !== after) {
        const files = tryGit(MEMORY_DIR, ['diff', '--name-only', before, after]).out.split('\n').filter(Boolean);
        shout.push(`memory: ${files.length} file(s) changed on the other machine`);
        notes.push([
          `The shared memory store (${MEMORY_DIR}) changed since this chat last synced; it was pulled just now. Changed files: ${files.join(', ')}.`,
          ...indexChanges(before, after),
          'The MEMORY.md loaded at the start of this chat is stale: re-read MEMORY.md and any changed memory relevant to the request before acting on it.',
        ].join('\n'));
      }
    }
  }

  if (isRepo(PROJECT_DIR)) {
    const fetch = tryGit(PROJECT_DIR, ['fetch', '--quiet', 'origin'], { timeout: NETWORK_TIMEOUT_MS });
    const ab = fetch.ok ? aheadBehind(PROJECT_DIR) : null;
    if (ab && ab.behind) {
      const name = path.basename(PROJECT_DIR);
      const branch = tryGit(PROJECT_DIR, ['rev-parse', '--abbrev-ref', 'HEAD']).out;
      const dirty = tryGit(PROJECT_DIR, ['status', '--porcelain']).out.split('\n').filter(Boolean).length;
      const log = tryGit(PROJECT_DIR, ['log', '--oneline', '--no-decorate', '-15', 'HEAD..@{u}']).out;
      shout.push(`${name} ${branch}: ${ab.behind} new commit(s) on origin`);
      notes.push([
        `Checkout ${PROJECT_DIR} on '${branch}' is ${ab.behind} commit(s) behind origin (${ab.ahead} unpushed, ${dirty} uncommitted file(s)). The other machine pushed:`,
        log,
        'Before acting on the request: tell the user, pull (--ff-only when possible), and rebuild or restart whatever runs from this checkout.',
      ].join('\n'));
    }
  }

  emit('UserPromptSubmit', {
    systemMessage: shout.length ? shout.join(' · ') : undefined,
    context: notes.length ? notes.join('\n\n') : undefined,
  });
}

function start() {
  touchSession(readHookInput().session_id);
  const notes = [];
  const shout = [];

  if (isRepo(MEMORY_DIR)) {
    const before = tryGit(MEMORY_DIR, ['rev-parse', 'HEAD']).out;
    const pull = tryGit(MEMORY_DIR, ['pull', '--rebase', '--autostash', '--quiet']);
    if (!pull.ok) {
      tryGit(MEMORY_DIR, ['rebase', '--abort']);
      shout.push(`memory sync: pull failed (${pull.out}) - resolve in ${MEMORY_DIR}`);
    } else {
      const after = tryGit(MEMORY_DIR, ['rev-parse', 'HEAD']).out;
      if (before !== after) {
        const n = tryGit(MEMORY_DIR, ['rev-list', '--count', `${before}..${after}`]).out;
        shout.push(`memory: pulled ${n} commit(s) from the other machine`);
      }
    }
  } else {
    notes.push(`Memory dir ${MEMORY_DIR || '(no autoMemoryDirectory pin in .claude/settings.local.json)'} is not a git repo - cross-machine memory sync is OFF.`);
  }

  if (isRepo(PROJECT_DIR)) {
    tryGit(PROJECT_DIR, ['fetch', '--quiet', 'origin'], { timeout: 15000 });
    const branch = tryGit(PROJECT_DIR, ['rev-parse', '--abbrev-ref', 'HEAD']).out;
    const ab = aheadBehind(PROJECT_DIR);
    const dirty = tryGit(PROJECT_DIR, ['status', '--porcelain']).out.split('\n').filter(Boolean).length;
    if (ab && (ab.ahead || ab.behind)) {
      const parts = [];
      if (ab.behind) parts.push(`${ab.behind} behind`);
      if (ab.ahead) parts.push(`${ab.ahead} unpushed`);
      const name = path.basename(PROJECT_DIR);
      shout.push(`${name} ${branch}: ${parts.join(', ')} vs origin${dirty ? `, ${dirty} uncommitted file(s)` : ''}`);
      notes.push(`Checkout ${PROJECT_DIR} on '${branch}' is ${parts.join(' and ')} relative to its upstream (${dirty} uncommitted files). The user works on two machines (Windows PC + Mac): mention this and offer to pull (--ff-only) / push before starting work.`);
    }
  }

  emit('SessionStart', {
    systemMessage: shout.length ? shout.join(' · ') : undefined,
    context: notes.length ? notes.join('\n') : undefined,
  });
}

function stop() {
  if (!isRepo(MEMORY_DIR)) return;
  const dirty = tryGit(MEMORY_DIR, ['status', '--porcelain']).out;
  if (dirty) {
    const add = tryGit(MEMORY_DIR, ['add', '-A']);
    const commit = add.ok && tryGit(MEMORY_DIR, ['commit', '--quiet', '-m', `mem: auto-sync from ${HOST}`]);
    if (!commit || !commit.ok) return; // e.g. another session holds index.lock - next Stop retries
  }
  const ab = aheadBehind(MEMORY_DIR);
  if (!ab || !ab.ahead) return;
  const pull = tryGit(MEMORY_DIR, ['pull', '--rebase', '--quiet']);
  if (!pull.ok) {
    tryGit(MEMORY_DIR, ['rebase', '--abort']);
    emit('Stop', { systemMessage: `memory sync: rebase conflict, not pushed (${pull.out}) - resolve in ${MEMORY_DIR}` });
    return;
  }
  const push = tryGit(MEMORY_DIR, ['push', '--quiet']);
  if (!push.ok) emit('Stop', { systemMessage: `memory sync: push failed (${push.out}) - will retry next turn` });
}

const mode = process.argv[2];
try {
  if (mode === 'start') start();
  else if (mode === 'stop') stop();
  else if (mode === 'prompt') prompt();
  else { console.error('usage: node scripts/memory-sync.mjs start|stop|prompt'); process.exit(1); }
} catch (e) {
  const event = { start: 'SessionStart', stop: 'Stop', prompt: 'UserPromptSubmit' }[mode] ?? 'Stop';
  emit(event, { systemMessage: `memory-sync: ${e.message}` });
}
