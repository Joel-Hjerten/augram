// Packages Augram as a native app with Velopack (docs/release.md); one command per platform, like Eyeris' build:win and
// build:mac.
//
//   node scripts/package.mjs windows   on Windows (and in .github/workflows/release.yml): win-x64 into
//                                      artifacts/releases/win-x64 (Augram-win-Setup.exe, a per-user installer without
//                                      admin rights, plus the Velopack update files)
//   node scripts/package.mjs mac       on the Mac: osx-arm64 into artifacts/releases/osx-arm64 (Augram.app in
//                                      Augram-osx-Portable.zip, plus the Velopack update files). Signed and notarized when
//                                      AUGRAM_SIGN_IDENTITY ("Developer ID Application: ...") and AUGRAM_NOTARY_PROFILE (an
//                                      `xcrun notarytool store-credentials` profile) are set; unsigned, with a warning,
//                                      when they are not. AUGRAM_INSTALLER_IDENTITY ("Developer ID Installer: ...") adds
//                                      a signed Augram-osx-Setup.pkg; an unsigned run makes an unsigned .pkg.
//   node scripts/package.mjs mac --signed
//                                      the same with Joel's Mac's values (joelsMac below) where the variables are not
//                                      set: the signed build as one fixed command (Joel allows it in .claude/settings.json).
//
// Each run reads the version from the app project (<Version>, Directory.Build.props), publishes self-contained and
// ReadyToRun into artifacts/publish/<runtime> with -p:AugramChannel=Release, and runs the repo's vpk tool
// (.config/dotnet-tools.json, restored first) over that folder. A dotnet missing from PATH is taken from ~/.dotnet, where
// the Mac has it. No trimming: Avalonia's XAML loader and the
// DI container reach types by reflection. Both artifact folders are emptied first, so a stale file never ships and vpk
// never sees an older release beside the new one (no delta packages until auto-update exists). Every process is started
// with an argument array, never a shell string.
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { delimiter, dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const artifacts = join(root, "artifacts");
const appProject = join("src", "Augram.App");

const packId = "Augram";
const packTitle = "Augram";
const packAuthors = "Joel Hjerten";
const bundleId = "com.joelart.augram";

// Extra Info.plist keys for the macOS bundle, applied over the base keys in macInfoPlist. Empty: Augram shows a Dock
// icon while running. LSUIElement: true would make it a menu-bar-only app (no Dock icon, no app menu, no Cmd+Tab
// entry); that is Joel's decision (docs/release.md, open questions).
const infoPlistExtras = {};

const targets = {
  windows: {
    platform: "win32",
    elsewhere: "Windows packaging runs on Windows: run it there, or push a v*.*.* tag and let .github/workflows/release.yml build it.",
    runtime: "win-x64",
    channel: "win",
    mainExe: "Augram.exe",
    icon: join(appProject, "Icons", "augram.ico"),
  },
  mac: {
    platform: "darwin",
    elsewhere: "macOS packaging needs codesign, notarytool and pkgbuild, which exist only on a Mac: run this on the Mac.",
    runtime: "osx-arm64",
    channel: "osx",
    mainExe: "Augram",
    icon: join(appProject, "Icons", "augram.icns"),
  },
};

function fail(message) {
  console.error(`\npackage: ${message}`);
  process.exit(1);
}

function warn(lines) {
  console.warn(["", ...lines.map((line) => `WARNING: ${line}`), ""].join("\n"));
}

function display(args) {
  return args.map((arg) => (/[\s"]/.test(arg) ? JSON.stringify(arg) : arg)).join(" ");
}

/**
 * Puts the per-user .NET SDK on PATH when no dotnet is there: the Mac has it in ~/.dotnet only (CLAUDE.md), and neither
 * a terminal nor an agent shell finds it. DOTNET_ROOT goes with it so every child, vpk included, finds the runtime.
 */
function useHomeDotnetIfNeeded() {
  const name = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const pathDirs = (process.env.PATH ?? "").split(delimiter).filter(Boolean);
  if (pathDirs.some((dir) => existsSync(join(dir, name)))) return;
  const dotnetRoot = process.env.DOTNET_ROOT || join(homedir(), ".dotnet");
  if (!existsSync(join(dotnetRoot, name))) fail(`dotnet is neither on PATH nor in ${dotnetRoot}: install the .NET SDK (global.json).`);
  process.env.DOTNET_ROOT = dotnetRoot;
  process.env.PATH = [dotnetRoot, ...pathDirs].join(delimiter);
}

/** Runs a process from the repo root; fails the script on a non-zero exit. Returns stdout when captured. */
function run(command, args, { capture = false } = {}) {
  if (!capture) console.log(`\n> ${command} ${display(args)}`);
  const result = spawnSync(command, args, {
    cwd: root,
    encoding: "utf8",
    stdio: capture ? ["ignore", "pipe", "inherit"] : "inherit",
  });
  if (result.error) fail(`${command} could not start: ${result.error.message}`);
  if (result.status !== 0) fail(`${command} ${args[0]} failed (exit code ${result.status}).`);
  return capture ? result.stdout.trim() : "";
}

/** Like run, captured, but a failure is null instead of the end of the script (git is informational here). */
function tryCapture(command, args) {
  const result = spawnSync(command, args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
  return result.status === 0 ? result.stdout.trim() : null;
}

/** Empties a folder under artifacts/ (and only there). */
function emptyArtifactFolder(folder) {
  if (!folder.startsWith(artifacts + sep)) fail(`refusing to empty ${folder}: not under ${artifacts}`);
  rmSync(folder, { recursive: true, force: true });
  mkdirSync(folder, { recursive: true });
}

function readVersion() {
  const version = run(
    "dotnet",
    ["msbuild", appProject, "-getProperty:Version", "-p:Configuration=Release", "-p:AugramChannel=Release"],
    { capture: true },
  ).split(/\r?\n/).pop();
  // Velopack wants SemVer 2 (1.2.3, 1.2.3-beta.1); a four-part assembly version is not one.
  if (!/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(version)) fail(`<Version> "${version}" is not a SemVer 2 version.`);
  return version;
}

function reportGit(version) {
  const commit = tryCapture("git", ["rev-parse", "--short", "HEAD"]);
  const dirty = tryCapture("git", ["status", "--porcelain"]);
  const tags = (tryCapture("git", ["tag", "--points-at", "HEAD"]) ?? "").split(/\r?\n/);
  console.log(`Augram ${version} from commit ${commit ?? "(unknown)"}`);
  if (dirty) warn(["the working tree has uncommitted changes: this package is not what any commit builds."]);
  if (!tags.includes(`v${version}`)) {
    console.log(`(HEAD is not tagged v${version}; fine for a trial, but a release is cut from its tag, docs/release.md.)`);
  }
}

/** The vpk options for signing and notarizing on the Mac, from the environment; warnings for what is missing. */
/**
 * Joel's Mac (docs/release.md): the original Developer ID Application certificate by fingerprint (a second one shares its
 * name), Eyeris' notary profile, the Developer ID Installer certificate. Not secrets: every signed app names them.
 * `--signed` uses them where the AUGRAM_* variables are not set, so a signed build is one fixed command that a permission
 * rule can allow (Eyeris allows its own build the same way).
 */
const joelsMac = {
  identity: "0EBC5A7A9D2041BCC40DC1B6FC9CB9C1F28C9CCE",
  notaryProfile: "eyeris-notary",
  installerIdentity: "Developer ID Installer: Joel Hjertén (MN7V4KZF8M)",
};

function macSigning(signed) {
  const fallback = signed ? joelsMac : {};
  const identity = process.env.AUGRAM_SIGN_IDENTITY?.trim() || fallback.identity;
  const notaryProfile = process.env.AUGRAM_NOTARY_PROFILE?.trim() || fallback.notaryProfile;
  const installerIdentity = process.env.AUGRAM_INSTALLER_IDENTITY?.trim() || fallback.installerIdentity;
  if (!identity) {
    if (notaryProfile || installerIdentity) {
      fail("AUGRAM_NOTARY_PROFILE or AUGRAM_INSTALLER_IDENTITY is set without AUGRAM_SIGN_IDENTITY: Apple notarizes signed apps only.");
    }

    warn([
      "UNSIGNED macOS package: AUGRAM_SIGN_IDENTITY and AUGRAM_NOTARY_PROFILE are not set.",
      "It runs on this Mac, but Gatekeeper blocks a downloaded copy, and the Accessibility grant",
      "has to be given again after every build. Release builds are signed: docs/release.md.",
    ]);
    return [];
  }

  // Fail before the publish rather than after it: an identity that is missing or untrusted (no Developer ID G2
  // intermediate, Eyeris dev-environment-traps) is not listed as valid. An Installer identity is not a code-signing one,
  // so it is looked up without the codesigning policy.
  const checks = [[identity, ["find-identity", "-v", "-p", "codesigning"]]];
  if (installerIdentity) checks.push([installerIdentity, ["find-identity", "-v"]]);
  for (const [name, args] of checks) {
    const matches = (tryCapture("security", args) ?? "").split(/\r?\n/).filter((line) => line.includes(name));
    if (matches.length === 0) {
      fail(
        `"${name}" is not a valid identity in the keychain (security ${args.join(" ")}). ` +
          "If the certificate is installed but not listed, import the Developer ID G2 intermediate, docs/release.md.",
      );
    }

    // Two certificates under one name (a second one made at Apple and imported): codesign refuses an ambiguous name
    // halfway through vpk, after the publish. Its SHA-1 fingerprint names one.
    if (matches.length > 1) {
      fail(
        `"${name}" names ${matches.length} certificates in the keychain; set the variable to one fingerprint instead:\n` +
          matches.map((line) => `  ${line.trim()}`).join("\n"),
      );
    }
  }

  const options = ["--signAppIdentity", identity];
  if (notaryProfile) {
    options.push("--notaryProfile", notaryProfile);
  } else {
    warn(["signed but NOT notarized (AUGRAM_NOTARY_PROFILE is not set): Gatekeeper blocks a downloaded copy."]);
  }

  if (installerIdentity) {
    options.push("--signInstallIdentity", installerIdentity);
  } else {
    // An unsigned .pkg around a signed app cannot be notarized; the signed Augram.app ships in the Portable zip.
    options.push("--noInst");
    console.log("No AUGRAM_INSTALLER_IDENTITY: no .pkg this time; Augram.app ships in Augram-osx-Portable.zip.");
  }

  return options;
}

function plistValue(value) {
  if (value === true) return "<true/>";
  if (value === false) return "<false/>";
  if (Number.isInteger(value)) return `<integer>${value}</integer>`;
  const escaped = String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
  return `<string>${escaped}</string>`;
}

/**
 * The bundle's Info.plist, copied verbatim by vpk (--plist; checked with `vpk [osx] bundle` on Windows). vpk would
 * otherwise write a generic one; ours carries the bundle id, NSHighResolutionCapable (Retina rendering) and the app
 * category, and is where infoPlistExtras land. Apple's version keys take numbers only.
 */
function macInfoPlist(version, target) {
  const numeric = version.split("-")[0];
  const keys = {
    CFBundleDevelopmentRegion: "en",
    CFBundleDisplayName: packTitle,
    CFBundleExecutable: target.mainExe,
    CFBundleIconFile: "augram.icns",
    CFBundleIdentifier: bundleId,
    CFBundleInfoDictionaryVersion: "6.0",
    CFBundleName: packTitle,
    CFBundlePackageType: "APPL",
    CFBundleShortVersionString: numeric,
    CFBundleSignature: "????",
    CFBundleVersion: numeric,
    LSApplicationCategoryType: "public.app-category.utilities",
    NSHighResolutionCapable: true,
    ...infoPlistExtras,
  };
  const body = Object.entries(keys)
    .map(([key, value]) => `  <key>${key}</key>\n  ${plistValue(value)}`)
    .join("\n");
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">',
    '<plist version="1.0">',
    "<dict>",
    body,
    "</dict>",
    "</plist>",
    "",
  ].join("\n");
}

function formatSize(bytes) {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${(bytes / 1024).toFixed(1)} KB`;
}

const mode = process.argv[2];
const target = targets[mode];
if (!target) fail("usage: node scripts/package.mjs windows|mac [--signed]");
const signed = process.argv.includes("--signed");
if (signed && mode !== "mac") fail("--signed is for the Mac build (Windows packages are not code-signed yet).");
if (process.platform !== target.platform) fail(target.elsewhere);

useHomeDotnetIfNeeded();
const version = readVersion();
reportGit(version);
const signing = mode === "mac" ? macSigning(signed) : [];

const publishDir = join(artifacts, "publish", target.runtime);
const releaseDir = join(artifacts, "releases", target.runtime);
emptyArtifactFolder(publishDir);
emptyArtifactFolder(releaseDir);

run("dotnet", [
  "publish", appProject,
  "-c", "Release",
  "-r", target.runtime,
  "--self-contained",
  "-p:AugramChannel=Release",
  "-p:PublishReadyToRun=true",
  "-o", relative(root, publishDir),
]);

run("dotnet", ["tool", "restore"]);

const pack = [
  "tool", "run", "vpk", "pack",
  "--packId", packId,
  "--packVersion", version,
  "--packTitle", packTitle,
  "--packAuthors", packAuthors,
  "--packDir", relative(root, publishDir),
  "--mainExe", target.mainExe,
  "--icon", target.icon,
  "--runtime", target.runtime,
  "--channel", target.channel,
  "--outputDir", relative(root, releaseDir),
  "--skip-updates",
];
if (mode === "mac") {
  const plistDir = join(artifacts, "package", target.runtime);
  emptyArtifactFolder(plistDir);
  const plist = join(plistDir, "Info.plist");
  writeFileSync(plist, macInfoPlist(version, target));
  // No --bundleId: vpk refuses it beside --plist and takes the id from CFBundleIdentifier.
  pack.push("--plist", relative(root, plist), ...signing);
}

run("dotnet", pack);

const produced = readdirSync(releaseDir)
  .map((name) => ({ name, path: join(releaseDir, name) }))
  .filter((file) => statSync(file.path).isFile());
console.log(`\nAugram ${version} (${target.runtime}) is in ${relative(root, releaseDir)}:`);
for (const file of produced) console.log(`  ${file.name.padEnd(36)} ${formatSize(statSync(file.path).size).padStart(9)}`);

// assets.<channel>.json is vpk's own list of what to upload; the release carries the files it names, not the list.
const uploads = produced.filter((file) => !file.name.startsWith("assets.")).map((file) => relative(root, file.path).replaceAll("\\", "/"));
if (mode === "windows") {
  console.log("\nThe installer is not code-signed: SmartScreen says \"unknown publisher\" (docs/release.md).");
} else {
  console.log(`\nAttach to the GitHub release (after the Windows workflow made it):\n  gh release upload v${version} ${uploads.join(" ")} --clobber`);
}
