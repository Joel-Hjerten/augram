// Prints the latest GitHub Actions runs for this repo and, for failed runs,
// the failure annotations (failing test names come from GitHubActionsTestLogger).
// Needs no token for a public repo. Usage: node scripts/ci-status.mjs [count]
const repo = "Joel-Hjerten/augram";
const count = Number(process.argv[2] ?? 5);
const api = (path) =>
  fetch(`https://api.github.com/repos/${repo}${path}`, {
    headers: { Accept: "application/vnd.github+json", "User-Agent": "augram-ci-status" },
  }).then((r) => (r.ok ? r.json() : Promise.reject(new Error(`${r.status} ${path}`))));

const { workflow_runs: runs } = await api(`/actions/runs?per_page=${count}`);
for (const run of runs) {
  const sha = run.head_sha.slice(0, 7);
  console.log(`${run.conclusion ?? run.status}\t${sha}\t${run.created_at}\t${run.display_title}`);
  if (run.conclusion !== "failure") continue;
  const { check_runs: checks } = await api(`/commits/${run.head_sha}/check-runs`);
  for (const check of checks) {
    const annotations = await api(`/check-runs/${check.id}/annotations`);
    for (const a of annotations) {
      if (a.annotation_level !== "failure") continue;
      console.log(`    ${a.title ?? ""}`.trimEnd());
      console.log(`      ${a.message.split("\n").slice(0, 6).join("\n      ")}`);
    }
  }
}
