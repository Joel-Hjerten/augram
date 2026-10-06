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

function start() {
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
  else { console.error('usage: node scripts/memory-sync.mjs start|stop'); process.exit(1); }
} catch (e) {
  emit(mode === 'start' ? 'SessionStart' : 'Stop', { systemMessage: `memory-sync: ${e.message}` });
}
