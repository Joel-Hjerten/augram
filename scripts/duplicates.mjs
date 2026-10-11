// Duplicated code, found by jscpd (settings in .jscpd.json at the repository root: C# and XAML in src, tests and tools).
//
//   node scripts/duplicates.mjs              the clones this branch and the working tree add since they left origin/main,
//                                           each with the code it copies; run it before committing (about 3 s)
//   node scripts/duplicates.mjs --base <ref> the same against another commit (CI passes the push's previous head)
//   node scripts/duplicates.mjs --all        every clone in the repository, biggest first
//   --strict                                 exit 1 when there is a new clone (CI does not pass it yet)
//
// A new clone is copied code: fold it into what it copies (CLAUDE.md, "Don't reimplement — extend"), or say in the
// commit message why both stay. In GitHub Actions each new clone is also a warning on the run, which
// scripts/ci-status.mjs prints. Exit 0; 1 with --strict and a new clone; 2 when jscpd could not run.
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const jscpd = "jscpd@5.4.1";
const args = process.argv.slice(2);
const all = args.includes("--all");
const strict = args.includes("--strict");
const baseArg = args.includes("--base") ? args[args.indexOf("--base") + 1] : undefined;
const inActions = process.env.GITHUB_ACTIONS === "true";

const git = (...a) => execFileSync("git", a, { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
const tryGit = (...a) => {
  try {
    return git(...a);
  } catch {
    return null;
  }
};
const commit = (ref) => tryGit("rev-parse", "--verify", "--quiet", `${ref}^{commit}`);
const root = git("rev-parse", "--show-toplevel");

/** The commit to compare with: where HEAD left the base; on CI's shallow checkout, the base commit fetched alone. */
function resolveBase(ref) {
  // A push that creates a branch has no previous head (all zeros).
  if (!ref || /^0+$/.test(ref)) ref = "origin/main";
  let sha = commit(ref);
  if (!sha) {
    tryGit("fetch", "--no-tags", "--depth=1", "origin", ref === "origin/main" ? "main" : ref);
    sha = commit(ref === "origin/main" ? "FETCH_HEAD" : ref);
  }

  return sha && (tryGit("merge-base", "HEAD", sha) ?? sha);
}

/** Files that differ from the base (in the working tree, untracked ones included): the copy is the side found here. */
function changedFiles(base) {
  const tracked = tryGit("diff", "--name-only", base) ?? "";
  const untracked = tryGit("ls-files", "--others", "--exclude-standard") ?? "";
  return new Set(`${tracked}\n${untracked}`.split("\n").filter(Boolean));
}

function runJscpd(base) {
  const out = fs.mkdtempSync(path.join(os.tmpdir(), "augram-jscpd-"));
  const jscpdArgs = [jscpd, "--reporters", "json", "--output", out, "--absolute", "--silent", "--no-tips"];
  if (base) jscpdArgs.push("--baseline-from-ref", base);
  // One command line, because npx is npx.cmd on Windows, which Node starts only through a shell. The arguments are
  // quoted, npx itself is not: cmd then resolves npx.cmd's own folder to the current one and npx fails to start.
  const command = ["npx", ...["--yes", ...jscpdArgs].map((a) => `"${a}"`)].join(" ");
  const run = spawnSync(command, { cwd: root, encoding: "utf8", shell: true });
  const reportPath = path.join(out, "jscpd-report.json");
  if (run.status !== 0 || !fs.existsSync(reportPath)) {
    console.log(`jscpd did not run (exit ${run.status}):\n${run.stdout ?? ""}${run.stderr ?? ""}`.trimEnd());
    process.exit(2);
  }

  const report = JSON.parse(fs.readFileSync(reportPath, "utf8"));
  fs.rmSync(out, { recursive: true, force: true });
  return report;
}

// On Windows jscpd's absolute paths are verbatim ones (//?/C:/...); the prefix goes before they are made relative.
const relative = (file) => path.relative(root, file.name.replace(/^[\\/]{2}\?[\\/]/, "")).split(path.sep).join("/");
const place = (file) => `${relative(file)}:${file.start}-${file.end}`;
// Workflow command data: %, CR and LF are escaped (GitHub's rules).
const escapeData = (s) => s.replaceAll("%", "%25").replaceAll("\r", "%0D").replaceAll("\n", "%0A");

const base = all ? null : resolveBase(baseArg);
const report = runJscpd(base);
const total = report.statistics.total;
console.log(`${total.clones} clones; ${total.duplicatedLines} of ${total.lines} lines duplicated (${total.percentage.toFixed(2)}%)`);

if (all) {
  for (const d of [...report.duplicates].sort((a, b) => b.lines - a.lines)) {
    console.log(`${String(d.lines).padStart(4)} lines  ${place(d.firstFile)}  ~  ${place(d.secondFile)}`);
  }

  process.exit(0);
}

if (!base) {
  console.log(`No base commit to compare with (${baseArg ?? "origin/main"} not found); --all lists every clone.`);
  process.exit(0);
}

const changed = changedFiles(base);
const fresh = report.duplicates.filter((d) => d.isNew).sort((a, b) => b.lines - a.lines);
if (fresh.length === 0) {
  console.log(`No new duplicated code since ${base.slice(0, 7)}.`);
  process.exit(0);
}

console.log(`New since ${base.slice(0, 7)}: ${fresh.length} (fold each into what it copies, or say in the commit message why both stay)`);
for (const d of fresh) {
  // The copy is the side in a changed file; when both changed, the first.
  const [copy, original] = !changed.has(relative(d.firstFile)) && changed.has(relative(d.secondFile))
    ? [d.secondFile, d.firstFile]
    : [d.firstFile, d.secondFile];
  console.log(`${String(d.lines).padStart(4)} lines  ${place(copy)}  copies  ${place(original)}`);
  if (inActions) {
    const message = `${d.lines} lines also at ${place(original)}. Reuse that code, or say in the commit message why both stay.`;
    console.log(`::warning file=${relative(copy)},line=${copy.start},endLine=${copy.end},title=Duplicated code::${escapeData(message)}`);
  }
}

process.exit(strict ? 1 : 0);
