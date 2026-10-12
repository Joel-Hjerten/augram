// Where did each subagent's wall-clock time go?
// Usage: node scripts/agent-time.mjs <projectTranscriptDir> [sinceISO]
// <projectTranscriptDir> is ~/.claude/projects/<this project> (each session folder there holds subagents/agent-*.jsonl).
// Prints, per subagent run since sinceISO, wall time split into tool and model time, then a total by tool.
// Plan 0007 close-out used it to compare with the 2026-10-11 baseline.
import fs from 'node:fs';
import path from 'node:path';

const root = process.argv[2];
const since = process.argv[3] ? Date.parse(process.argv[3]) : 0;

function classify(name, input) {
  if (name === 'Bash' || name === 'PowerShell') {
    const c = (input.command || '').toLowerCase();
    if (/dotnet\s+test|dotnet\.exe"?\s+test/.test(c)) return 'cmd: dotnet test';
    if (/dotnet\s+build|dotnet\.exe"?\s+build/.test(c)) return 'cmd: dotnet build';
    if (/dotnet\s+format/.test(c)) return 'cmd: dotnet format';
    if (/dotnet\s+run|uishots/.test(c)) return 'cmd: dotnet run/UiShots';
    if (/ci-status/.test(c)) return 'cmd: ci-status wait';
    if (/^\s*git|\bgit\s/.test(c)) return 'cmd: git';
    if (/\bnode\b/.test(c)) return 'cmd: node';
    return 'cmd: other';
  }
  return name;
}

function analyse(file) {
  const lines = fs.readFileSync(file, 'utf8').split('\n').filter(Boolean);
  const msgs = [];
  for (const l of lines) { try { msgs.push(JSON.parse(l)); } catch {} }
  const ts = msgs.map(m => Date.parse(m.timestamp)).filter(n => !isNaN(n));
  if (!ts.length) return null;
  const start = Math.min(...ts), end = Math.max(...ts);
  const pending = new Map(); // tool_use_id -> {t, cat}
  const byCat = {};
  let toolTime = 0, nTools = 0, docReads = 0, docReadFiles = new Set();
  let testRuns = 0, buildRuns = 0, outTokens = 0, turns = 0;
  for (const m of msgs) {
    const t = Date.parse(m.timestamp);
    const content = m.message?.content;
    if (m.type === 'assistant') {
      turns++;
      outTokens += m.message?.usage?.output_tokens || 0;
      if (Array.isArray(content)) for (const b of content) {
        if (b.type === 'tool_use') {
          const cat = classify(b.name, b.input || {});
          pending.set(b.id, { t, cat });
          nTools++;
          if (cat === 'cmd: dotnet test') testRuns++;
          if (cat === 'cmd: dotnet build') buildRuns++;
          if (b.name === 'Read' && /docs[\\/]|README\.md|CLAUDE\.md/i.test(b.input?.file_path || '')) { docReads++; docReadFiles.add(path.basename(b.input.file_path)); }
        }
      }
    } else if (m.type === 'user' && Array.isArray(content)) {
      for (const b of content) {
        if (b.type === 'tool_result' && pending.has(b.tool_use_id)) {
          const p = pending.get(b.tool_use_id);
          const d = Math.max(0, t - p.t);
          byCat[p.cat] = byCat[p.cat] || { n: 0, ms: 0 };
          byCat[p.cat].n++; byCat[p.cat].ms += d; toolTime += d;
          pending.delete(b.tool_use_id);
        }
      }
    }
  }
  return { start, end, wall: end - start, toolTime, byCat, nTools, docReads, docReadFiles: [...docReadFiles], testRuns, buildRuns, outTokens, turns };
}

const min = ms => (ms / 60000).toFixed(1);
const all = [];
for (const sess of fs.readdirSync(root)) {
  const sub = path.join(root, sess, 'subagents');
  if (!fs.existsSync(sub)) continue;
  for (const f of fs.readdirSync(sub).filter(f => f.endsWith('.jsonl'))) {
    const metaF = path.join(sub, f.replace('.jsonl', '.meta.json'));
    const meta = fs.existsSync(metaF) ? JSON.parse(fs.readFileSync(metaF, 'utf8')) : {};
    const r = analyse(path.join(sub, f));
    if (!r || r.start < since) continue;
    all.push({ desc: meta.description || f, type: meta.agentType, ...r });
  }
}
all.sort((a, b) => a.start - b.start);

const totals = {};
let wallSum = 0, toolSum = 0;
for (const a of all) {
  wallSum += a.wall; toolSum += a.toolTime;
  for (const [k, v] of Object.entries(a.byCat)) { totals[k] = totals[k] || { n: 0, ms: 0 }; totals[k].n += v.n; totals[k].ms += v.ms; }
  const top = Object.entries(a.byCat).sort((x, y) => y[1].ms - x[1].ms).slice(0, 4).map(([k, v]) => `${k} ${min(v.ms)}m/${v.n}`).join(', ');
  console.log(`${new Date(a.start).toISOString().slice(5, 16)} ${min(a.wall).padStart(5)}m wall | tools ${min(a.toolTime)}m | model ${min(a.wall - a.toolTime)}m | ${a.nTools} calls, ${a.turns} turns, ${a.outTokens} outTok | test x${a.testRuns} build x${a.buildRuns} | docReads ${a.docReads} | ${a.desc}`);
  console.log(`      top: ${top}`);
}
console.log(`\nAgents: ${all.length}; wall ${min(wallSum)}m; tool ${min(toolSum)}m; model/think ${min(wallSum - toolSum)}m`);
for (const [k, v] of Object.entries(totals).sort((x, y) => y[1].ms - x[1].ms)) console.log(`  ${k.padEnd(26)} ${min(v.ms).padStart(7)}m  ${String(v.n).padStart(5)} calls  avg ${(v.ms / v.n / 1000).toFixed(0)}s`);
