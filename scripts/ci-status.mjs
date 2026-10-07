// GitHub Actions status for this repo, through the public REST API (no token needed for a public repo).
//
//   node scripts/ci-status.mjs [count]      the latest runs; for a failed or running run, each job (one per runner OS)
//                                           and, for a failed job, its failing tests (from GitHubActionsTestLogger)
//   node scripts/ci-status.mjs --wait [sha] waits for the run of a commit (default HEAD) to finish, one call a minute,
//                                           then prints its jobs; exit 0 green, 1 red, 2 unknown (rate limited, no run
//                                           appeared, or still running after 30 minutes)
//
// Anonymous calls are limited to 60 an hour per address; every report ends with the calls left. A 403 or 429 at the
// limit is reported as "unknown until HH:MM", never as finished. GITHUB_TOKEN or GH_TOKEN, when set, raises the limit.
import { execFileSync } from "node:child_process";

const repo = "Joel-Hjerten/augram";
const token = process.env.GITHUB_TOKEN ?? process.env.GH_TOKEN;
const pollMs = 60_000;
const waitLimitMs = 30 * 60_000;
let remaining = null;

class RateLimited extends Error {
  constructor(resetAt) {
    super(`rate limited until ${resetAt.toTimeString().slice(0, 5)}`);
    this.resetAt = resetAt;
  }
}

async function api(path) {
  const response = await fetch(`https://api.github.com/repos/${repo}${path}`, {
    headers: {
      Accept: "application/vnd.github+json",
      "User-Agent": "augram-ci-status",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
  remaining = response.headers.get("x-ratelimit-remaining") ?? remaining;
  if ((response.status === 403 || response.status === 429) && response.headers.get("x-ratelimit-remaining") === "0") {
    throw new RateLimited(new Date(Number(response.headers.get("x-ratelimit-reset")) * 1000));
  }

  if (!response.ok) {
    throw new Error(`${response.status} ${path}`);
  }

  return response.json();
}

const shortSha = (sha) => sha.slice(0, 7);
const outcome = (item) => item.conclusion ?? item.status;

/** One line per job (its name carries the runner OS); the failing tests under each failed job. */
async function printJobs(run) {
  const { jobs } = await api(`/actions/runs/${run.id}/jobs`);
  for (const job of jobs) {
    console.log(`    ${job.name}: ${outcome(job)}`);
    if (job.conclusion !== "failure") continue;
    const annotations = await api(`/check-runs/${job.id}/annotations`);
    for (const a of annotations) {
      if (a.annotation_level !== "failure") continue;
      console.log(`      ${a.title ?? ""}`.trimEnd());
      console.log(`        ${a.message.split("\n").slice(0, 6).join("\n        ")}`);
    }
  }
}

async function list(count) {
  const { workflow_runs: runs } = await api(`/actions/runs?per_page=${count}`);
  for (const run of runs) {
    console.log(`${outcome(run)}\t${shortSha(run.head_sha)}\t${run.created_at}\t${run.display_title}`);
    if (run.conclusion === "failure" || run.status !== "completed") {
      await printJobs(run);
    }
  }

  return 0;
}

async function wait(sha) {
  const started = Date.now();
  let announced = false;
  while (Date.now() - started < waitLimitMs) {
    const { workflow_runs: runs } = await api(`/actions/runs?head_sha=${sha}&per_page=1`);
    const run = runs[0];
    if (run?.status === "completed") {
      console.log(`${run.conclusion}\t${shortSha(sha)}\t${run.display_title}`);
      await printJobs(run);
      return run.conclusion === "success" ? 0 : 1;
    }

    if (!announced) {
      console.log(run ? `waiting for ${shortSha(sha)} (${run.status})` : `no run for ${shortSha(sha)} yet; waiting`);
      announced = true;
    }

    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }

  console.log(`unknown\t${shortSha(sha)}\tstill not finished after ${waitLimitMs / 60_000} minutes`);
  return 2;
}

const args = process.argv.slice(2);
let code;
try {
  code = args[0] === "--wait"
    ? await wait(args[1] ?? execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim())
    : await list(Number(args[0] ?? 5));
} catch (error) {
  if (!(error instanceof RateLimited)) throw error;
  console.log(`unknown\tGitHub API ${error.message}; CI status cannot be read until then`);
  code = 2;
}

console.log(`(${remaining ?? "?"} API calls left this hour${token ? ", with token" : ""})`);
process.exit(code);
