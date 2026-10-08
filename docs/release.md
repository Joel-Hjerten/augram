# Releasing Augram

Augram ships as a native app through [Velopack](https://velopack.io) (MIT), the .NET counterpart of the electron-builder
setup in Eyeris: a per-user Windows installer that needs no admin rights, a signed and notarized macOS `Augram.app`, and
the files a future updater reads. One GitHub release per tag `vX.Y.Z` carries both platforms: the Windows files come from
CI, the macOS files from Joel's Mac, because signing needs the Developer ID identity in its keychain.

| Piece | Where |
|---|---|
| Version | `<Version>` in `Directory.Build.props`, SemVer 2 (`0.2.0`, `0.3.0-beta.1`). Velopack needs every release higher than the last. |
| Installer hooks | `VelopackApp.Build().Run()`, the first line of `Main` in `src/Augram.App/Program.cs`. It must stay first: Setup, updates and uninstall start `Augram.App.exe` with hook arguments, and it answers them and exits before the single-instance guard or the UI exist. `vpk pack` checks the call is there. |
| Packaging | `node scripts/package.mjs windows` / `mac` (header comment: what it runs). vpk is a repo-local tool, `.config/dotnet-tools.json`; `dotnet tool restore` once per clone. Package and tool versions move together (`Directory.Packages.props`). |
| Release workflow | `.github/workflows/release.yml`, on a pushed tag `v*.*.*` |
| Output | `artifacts/publish/<runtime>` (the published app) and `artifacts/releases/<runtime>` (what ships), both emptied by every run, both gitignored |

## What a build is

`dotnet publish -c Release -r <runtime> --self-contained -p:AugramChannel=Release -p:PublishReadyToRun=true`: no .NET
install needed on the target, ReadyToRun for start-up time, the Release build channel. **No trimming**: Avalonia's XAML
loader and the DI container reach types by reflection, and a trimmed build fails at run time, not at build time
(`PublishTrimmed` is pinned off in `Augram.App.csproj`). Debug symbols and XML doc files stay out of the package. The
Windows app folder is about 118 MB; the package about 55 MB.

| File | Platform | What it is |
|---|---|---|
| `Augram-win-Setup.exe` | Windows | The installer (about 62 MB, the full package inside). Installs for the current user, no admin prompt, starts Augram when done. |
| `Augram-win-Portable.zip` | Windows | The same app as a folder that runs without installing. |
| `Augram-osx-Portable.zip` | macOS | `Augram.app`, signed and notarized in a release build. The macOS download. |
| `Augram-osx-Setup.pkg` | macOS | An installer package; only when `AUGRAM_INSTALLER_IDENTITY` is set (a signed one) or nothing is signed (a local trial). |
| `Augram-X.Y.Z-full.nupkg`, `Augram-X.Y.Z-osx-full.nupkg` | both | The full package an updater downloads. |
| `releases.win.json`, `RELEASES`, `releases.osx.json` | both | The update feed per platform (`RELEASES` is the legacy Squirrel form). |
| `assets.<channel>.json` | both | vpk's own upload list; not attached to the release. |

The file names carry the platform (vpk's channels `win` and `osx`), so both sets sit in one GitHub release without
clashing. `package.mjs` prints what it produced with sizes, and on the Mac the exact upload command.

## Cutting a release

1. Set `<Version>` in `Directory.Build.props` (say `0.2.0`), commit, push, and let CI go green.
2. Tag and push the tag: `git tag v0.2.0` then `git push origin v0.2.0`.
3. CI (`release.yml`, windows-latest) checks the tag against `<Version>` and stops if they differ, runs the tests, runs
   `node scripts/package.mjs windows`, and creates the GitHub release `v0.2.0` with the Windows files attached (a
   version with a `-` suffix becomes a prerelease). A re-run replaces the files in the existing release.
4. On the Mac, once that release exists:

   ```sh
   git fetch --tags && git checkout v0.2.0
   dotnet tool restore
   export AUGRAM_SIGN_IDENTITY="Developer ID Application: <name> (<team id>)"
   export AUGRAM_NOTARY_PROFILE=<notarytool profile>
   node scripts/package.mjs mac
   gh release upload v0.2.0 artifacts/releases/osx-arm64/Augram-0.2.0-osx-full.nupkg \
     artifacts/releases/osx-arm64/Augram-osx-Portable.zip artifacts/releases/osx-arm64/releases.osx.json --clobber
   git checkout main
   ```

   The script prints the upload line with the real file names; copy it from there. Without `gh` (`brew install gh`, then
   `gh auth login`), drag the same files onto the release's edit page on GitHub.

A wrong tag (CI says it does not match `<Version>`, and no release was made): delete it locally and on GitHub
(`git tag -d v0.2.0`, `git push origin :refs/tags/v0.2.0`), fix `<Version>`, commit, tag again. Never move a tag that
already has a release; bump the version instead.

A trial build needs no tag: `node scripts/package.mjs windows` on the PC works from any checkout and says when HEAD is
not tagged or the tree has uncommitted changes. Do not run the Setup.exe on a machine where you are developing unless
you mean to install Augram there.

## The Mac, once

The same identity and notary profile as Eyeris, so on a Mac that already signs Eyeris most of this is done.

1. **Developer ID Application certificate** in the login keychain. `security find-identity -v -p codesigning` must list
   it as valid. **If the certificate is installed but the list shows 0 valid identities**, the Developer ID **G2**
   intermediate is missing (Xcode installs it; a Mac without Xcode has only the 2012 G1 one): fetch
   `https://www.apple.com/certificateauthority/DeveloperIDG2CA.cer` and `security import` it into the login keychain
   (Eyeris `docs/learnings/dev-environment-traps.md`, "Developer ID on a Mac without Xcode"). `package.mjs` checks the
   identity before it builds and says this. Keep the identity exported as a `.p12` somewhere that is not the Mac.
2. **Notarization credentials**, stored once: `xcrun notarytool store-credentials <profile>` (Apple ID, team id, an
   app-specific password). `AUGRAM_NOTARY_PROFILE` names that profile; vpk passes it to `notarytool --keychain-profile`,
   waits for Apple, and staples the ticket.
3. Optional, for a signed `.pkg`: a **Developer ID Installer** certificate, named in `AUGRAM_INSTALLER_IDENTITY`. Without
   it a signed run skips the `.pkg` (an unsigned package around a signed app cannot be notarized) and `Augram.app` ships
   in the zip, which is how most Mac apps outside the App Store arrive.

Without `AUGRAM_SIGN_IDENTITY` the script still packs, unsigned, after a warning: fine for a trial on this Mac, but
Gatekeeper blocks a downloaded copy, and macOS asks for the Accessibility grant again after every build.

vpk signs with the hardened runtime and, since none is given, its default .NET entitlements (`allow-jit`,
`allow-unsigned-executable-memory`, `disable-library-validation`, `allow-dyld-environment-variables`,
`automation.apple-events`). A custom entitlements file would go in with `--signEntitlements`.

**Info.plist.** `package.mjs` writes the bundle's Info.plist itself (`macInfoPlist`): bundle id `com.joelart.augram`,
name, version, icon `augram.icns`, `NSHighResolutionCapable`, the Utilities category. vpk copies it verbatim and refuses
`--bundleId` beside `--plist`, so the id lives in the plist. Extra keys go in `infoPlistExtras` at the top of the script;
it is empty, so Augram shows a Dock icon while it runs. `LSUIElement: true` there would make it a menu-bar-only app (no
Dock icon, no app menu, no Cmd+Tab entry): Joel's decision.

## Installing, where things live, uninstalling

| | Windows | macOS |
|---|---|---|
| App | `%LocalAppData%\Augram` (the app in `current\`, Velopack's `Update.exe` beside it); Start menu and desktop shortcuts "Augram" | `Augram.app` wherever it is dragged from the zip, normally Applications |
| Config and logs | `%APPDATA%\Augram` (`augram.json`, `logs\`) | `~/Library/Application Support/Augram` |
| Uninstall | Settings › Apps › Installed apps › Augram › Uninstall | Quit Augram, delete `Augram.app`, remove it under System Settings › Privacy & Security › Accessibility |

The config folder is the same for the installed app and for dev builds, so both read and write Joel's real gestures and
commands, and an uninstall leaves it alone (delete the folder by hand for a clean slate). Start at login stores the
running exe's path (`%LocalAppData%\Augram\current\Augram.App.exe` when installed), which stays the same across updates.

**SmartScreen.** The Windows installer is not code-signed yet, so a downloaded `Augram-win-Setup.exe` gets "Windows
protected your PC" with "Unknown publisher": More info › Run anyway. With Smart App Control on, Windows may refuse it
outright. The cure is a code-signing certificate: Azure Trusted Signing (`vpk pack --azureTrustedSignFile`) or a
certificate through `--signParams`; not decided.

**Gatekeeper and Accessibility on the Mac.** A notarized `Augram.app` opens without a warning. The Accessibility grant
(System Settings › Privacy & Security › Accessibility) belongs to the signed identity and bundle id, so it survives
updates of a signed build; an unsigned build needs it again every time. A dev build launched from VS Code uses VS Code's
grant (session handoff §8a); the bundle needs its own.

## Traps

- **`spctl --assess` says "rejected" for an unsigned local build.** Normal: nothing quarantines a file built on the Mac,
  so it runs anyway; the assessment bites only a downloaded copy (Eyeris, same finding).
- **A launch that seems to do nothing** may have handed off to an Augram already running (the single-instance guard,
  App README "Tray and single instance"), installed or dev. Quit the other one first.
- **A version can ship once.** Velopack orders releases by version; re-releasing `0.2.0` with different contents
  confuses an updater. Bump instead.
- **The first packaging run needs the network** for the runtime packs of the target runtime and the vpk tool; later runs
  use the NuGet cache.

## Not built yet

- **Auto-update.** Nothing checks for updates. It would be a `Hosting/` service in Augram.App, registered in
  `CompositionRoot`, holding Velopack's `UpdateManager` over a `GithubSource` for `https://github.com/Joel-Hjerten/augram`
  (it reads the latest release's `releases.<channel>.json`): check at launch and daily, download, and apply on the next
  restart or when Joel says so. With it, `release.yml` should first `vpk download github` the previous release into the
  output folder so `vpk pack` also writes a small delta package, and `package.mjs` should stop emptying
  `artifacts/releases`. Open for Joel: when to check, ask or silent, prereleases or not.
- **Windows code signing** (above).
- **Start at login after an uninstall.** The Run-key entry stays behind pointing at a deleted exe (Windows skips it).
  Velopack's uninstall hook (`VelopackApp.Build().OnBeforeUninstallFastCallback(...)`) could remove it.
