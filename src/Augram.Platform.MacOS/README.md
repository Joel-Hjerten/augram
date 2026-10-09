# Augram.Platform.MacOS

macOS implementations of the same Core ports as `Augram.Platform.Windows`, through CoreGraphics, the Accessibility API (`AXUIElement`) and a handful of AppKit calls via the Objective-C runtime. Started 2026-10-07 on Joel's Mac (Apple silicon, built-in Retina display at 2x plus an external display at 1x, "Displays have separate Spaces" on).

**May reference:** `Augram.Core` only. AppKit / Accessibility interop goes here and nowhere else.

**Must never contain:** Avalonia, SharpHook, business rules, or Windows types.

## What is here

| Folder / file | Port | What it does |
|---|---|---|
| `Interop/MacNative` | | The only P/Invoke surface: CoreFoundation, CoreGraphics, HIServices (Accessibility), libobjc, libproc. `LibraryImport`, blittable types, bools as bytes. |
| `Interop/Cf`, `Interop/Ax`, `Interop/ObjC` | | Wrappers: constant `CFString`s, reading CF values, AX attribute get/set/press with a 0.5 s messaging timeout per app, `AXError` → log reason, `NSRect` returns (arm64 vs x86-64), autorelease pools. |
| `WindowSystem/MacWindowSystem` | `IWindowSystem` | `WindowAt` hit-tests the window server's list (`CGWindowListCopyWindowInfo`, front to back; no app is asked). `Foreground` is the focused window of the focused app (AX). `Activate` applies A20, then raises the window and sets the app `AXFrontmost` (works from a background app; `NSRunningApplication.activate` does not since macOS 14). The ignore list's cheap keys (**compiled on Windows, not yet run on a Mac**): `WindowKeyAt` is the `CGWindowID` `MacWindowPick` finds in a copy of the window list at most 250 ms old (`MacWindowKeys`, pure and tested on every OS), `ForegroundKey` the frontmost app's pid (`Ax.FocusedApplicationPid`: the system-wide focused application, whose pid is read locally, so no app is asked); a new frontmost pid drops the list copy. |
| `WindowSystem/MacWindowOperations` | `IWindowOperations` | Close, Minimize, MaximizeOrRestore. The rest report not supported until built (table below). |
| `WindowSystem/MacWindowPick`, `MacRect`, `MacMaximize` | | Pure rules, tested on every OS: which window a point hits, desktop and full-screen flags, the Cocoa ↔ top-left flip, what counts as filled, which screen a window is on. |
| `WindowSystem/MacWindowList`, `MacScreens` | | Native readers: window list, display bounds, process path (`proc_pidpath`); `NSScreen` frames and visible frames flipped to top-left points. |
| `Overlay/MacOverlayWindowStyle` | `IOverlayWindowStyle` | `ignoresMouseEvents = YES`, read back (invariant 6); status-window level (over the Dock and menu bar); all Spaces and full-screen apps; out of the Cmd-` cycle. `Place` sets the frame in points. |
| `Input/MacCursorProbe` | `ICursorProbe` | `CGEventGetLocation` of a blank event. |
| `Launch/MacProcessLauncher` | `IProcessLauncher` | The Run step. `MacStartInfo` (pure, tested on every OS) expands `%VAR%` and a leading `~`, then: a URI → `open <uri>`; an executable file, or a bare name the PATH resolves to one → the executable itself with the arguments, in the Start in folder or the home folder; an app by name (`Safari`) or a `.app` path → `open -a <app> --args <arguments>`; a document or folder → `open <path>` (arguments ignored). Hidden adds `-g -j` (not brought to the front, launched hidden). Elevated → not supported ("running as administrator is not supported on macOS"; the step skips). `MacProcessStart` (native, untested) finds executables and starts the process; it waits at most 2 s for `open` itself (never the app) so an unknown app fails with `open`'s message. Tests use the internal constructor with a fake runner; none starts a process. |
| `Display/MacDisplayModes` | `IDisplayModes` | The Display steps (learnings 0002 §3; **compiled on Windows, not yet run on a Mac**). Lists the active displays (`CGGetActiveDisplayList`, `CGDisplayBounds` in global points, `CGMainDisplayID`, `CGDisplayIsBuiltin`) with every mode of `CGDisplayCopyAllDisplayModes` including the duplicate low-resolution ones. A resolution is points (the "looks like" size), a rate CoreGraphics' own rounded to three decimals (0 = not reported). `SetMode` finds the same `CGDisplayMode` again, prefers the HiDPI twin (`MacModePick`, pure and tested on every OS) and applies it in one `CGBeginDisplayConfiguration` transaction completed `kCGConfigurePermanently` (like System Settings), cancelling on error. HDR: no public API, so `CanSwitchHdr` is false and every display reads unsupported. CoreGraphics only, no AppKit, so it runs on the calling thread (executor, or the UI thread for the step form); the Mac session should confirm a mode change from the executor thread is fine and that the modes listed match System Settings' "Show all resolutions". |
| `Display/MacDisplayList`, `MacNativeMode`, `MacDisplayReading` | | Native reader and applier (`Interop/MacNative.Display.cs`), and the plain records it returns. |
| `Clipboard/MacClipboard` | `IClipboard` | The Clear clipboard step (**compiled on Windows, not yet run on a Mac**): `[[NSPasteboard generalPasteboard] clearContents]` in an autorelease pool, on the command executor thread (the pasteboard is a server-side object; no main-thread hop). No `NSPasteboard` class (AppKit not loaded) → not supported, the step skips. Untested by design: a test would clear the real pasteboard. The Mac session should confirm a paste after the step has nothing to paste, from Finder and from a text field. |
| `MacAccessibility` | | `IsTrusted()`; the App logs it once at engine start. |
| `MacStatusItem` | | Augram's menu-bar item (2026-10-09; the App's `Tray/MacTrayHost` uses it instead of Avalonia's): an `NSStatusItem` whose button sends its action on a left or right mouse-up to a small runtime-registered `NSObject` subclass (`AugramStatusItemTarget`, methods implemented by `UnmanagedCallersOnly` callbacks). A left click raises `Clicked`; a right click or a Control-click builds an `NSMenu` from `MenuEntries` (`MacMenuEntry`: title, check mark, enabled, action; the item's tag is its index), attaches it, `performClick:`s the button and detaches it again. The image is the tray PNG (36 px) at 18 points, template or colour. Main thread only. **Not testable without a menu bar**: click, double click, right click and Control-click checked by hand (Joel, 2026-10-09, dev build). |

## Ignore list on macOS (2026-10-09, not yet run on a Mac)

The engine's ignore-list watch (`src/Augram.Engine/README.md`, "Ignore list") runs the same code on both platforms; only the two cheap keys above are Mac-specific. An ignored app matches on its macOS executable names, or on the known-app guess from its Windows names (`Core/Mapping/KnownApps`); an entry with only Windows names and no guess (`vmware.exe`) matches nothing on a Mac, and while no active entry can match here nothing is watched at all. The Mac session must verify:

1. Over an ignored app (Blender, or any app added on the Ignored tab by its macOS name), a right-drag reaches the app untouched: no trail, the app's own drag works, and the wheel while the button is held scrolls the app.
2. A "disable while focused" app (VMware Fusion, executable `VMware Fusion`; or TextEdit for the test) pauses Augram while it is frontmost: the log says `Paused while an ignored app has focus`, the menu-bar tooltip says "Augram (paused: … is focused)", and gestures work again (`Resumed: …`) within about 200 ms of another app coming to the front.
3. Idle cost: with the ignore list watched and the pointer still, Activity Monitor shows Augram near 0% CPU (the watch asks `Ax.FocusedApplicationPid` every 200 ms); while moving the pointer it stays low (the window list is read at most every 250 ms, plus once per window entered).
4. `Ax.FocusedApplicationPid` answers without the Accessibility prompt beyond the one Augram already needs, and gives Augram's own pid when its window is in front. **Found 2026-10-09: it crashed.** With Augram in front, the system-wide query is answered in-process on the calling thread through AppKit and Avalonia's window; on `augram-ignore-watch` that was EXC_BAD_ACCESS in `-[AvnWindow automationPeer]` (three crashes at start of the installed 0.5.1, right after a take-over showed the window). The system-wide query now always runs on the main thread (`Interop/Ax.Focus`).
5. The real executable names of the apps Joel ignores on the Mac (`Resolve` for DaVinci Resolve, `VMware Fusion`, `Blender`), so `KnownApps` can learn the pairs.

Not built yet: `ISystemEvents` (sleep/wake via `NSWorkspace` notifications), `IStartupRegistration` (`SMAppService`, needs an app bundle), the placement operations and an app bundle.

## Window finder on macOS (2026-10-09, not yet run on a Mac)

The identification form's magnifiers (App README, "Window finder") call `MacWindowSystem.WindowKeyAt` and `WindowAt` on the main thread while dragging: both read the window server's list and `proc_pidpath` only, no Accessibility call, so they run inline there. The pointer comes from Avalonia (`PointToScreen`), which is points from the top left of the main display like the window list. The Mac session must verify:

1. Dragging a magnifier from Commands › Apps out of Augram's window onto Safari, Chrome or TextEdit names the app in the popup as the pointer moves (on both displays: the 2x built-in and the 1x external, and on a display left of or above the main one), and the release adds `Safari` / `Google Chrome` / `TextEdit` to the macOS executables.
2. The cursor is a crosshair outside Augram's window during the drag, and the target app gets no click from the release.
3. Over Augram's own window the popup says so and nothing is picked; over the desktop it names whatever owns the desktop window there (Finder for the icons; say what the wallpaper reads as); over the menu bar it names the menu-bar owner (`MacWindowPick` keeps small windows above the normal layer).
4. Esc cancels; whether a right-click during the drag reaches Augram outside its window (AppKit may send it to the window under the pointer, in which case only Esc cancels).
5. Executable path fills the path inside the bundle (`/Applications/Safari.app/Contents/MacOS/Safari`); the title magnifier says "no title" without the Screen Recording permission.

## Coordinates and identity

- **Units are points, origin top-left of the main display, y down** everywhere this project hands a value out: the hook reports the pointer that way, CoreGraphics window bounds and the Accessibility API use it, and so does Avalonia for screens and window positions. Only `NSScreen` is bottom-left based; `MacRect.FromCocoa` flips it by the main screen's height. `WindowSize` is read as points.
- `WindowIdentity.Handle` and `RootHandle` are both the `CGWindowID` (a macOS window has no child windows to resolve). `ProcessName` is the executable's file name (`Safari`, `Google Chrome`, `Code`), the counterpart of `chrome.exe`; `ProcessPath` is the full path inside the bundle; `ClassChain` is empty; `Title` is null unless the process may record the screen (Screen Recording permission).
- The pointer is a window too on macOS 26: a 28 × 40 "Cursor" window of the window server at `kCGCursorWindowLevel` (2147483630) under the point (measured 2026-10-09: the window finder's crosshair made every drop name WindowServer). `MacWindowPick.At` skips that layer.
- `IsDesktop`: the hit window is below the normal layer (wallpaper, Finder's desktop icons). Window operations refuse the desktop. A smaller window on a higher layer (menu bar, status items) wins over the window behind it, so a gesture there never reaches a window the user was not pointing at. Display-sized windows above the normal layer are skipped: the Dock draws itself in a transparent window as large as its display, and overlay utilities do the same, so they would otherwise swallow every point on that display.
- A window operation finds the window again by its `CGWindowID` among its app's `AXWindows` (`_AXUIElementGetWindow`, private but what every macOS window manager uses); not found → "window gone".

## Window operations

| `WindowOperation` | macOS | State |
|---|---|---|
| `Close` | `AXPress` on the window's `AXCloseButton`: the app runs its own close path ("save changes?"); the process is never killed | built |
| `Minimize` | `AXMinimized` = true | built |
| `MaximizeOrRestore` | **macOS's own tiling (D6, Joel 2026-10-09):** brings the window forward (app `AXFrontmost`, window `AXMain`, `AXRaise`) and presses the app's Window › Move & Resize › **Fill**; on a window that fills the visible frame, **Return to Previous Size**. The items are found without titles (localised): the Window menu is the one holding ⌘M, the items are the enabled ones with key F or R and Control without Command, the fn bit (16) ignored (measured in VS Code: Fill is `F/28`) (`Interop/Ax.Menu`). Restoring a window Augram did not fill tries Return to Previous Size, then **Window › Zoom** (macOS's own zoom, from a title-bar double-click; found by its title or as the first item without a shortcut after Minimize), each kept only if the target window itself moved. One log line per press, and the Window menu's items and shortcuts when nothing matched. Without the items: Augram fills the visible frame itself and restores the frame it remembered; a filled window with nothing to return to gets two thirds of the visible frame, centred (`MacMaximize.DefaultRestore`). Native full screen is left (`AXFullScreen` = false). Never `AXZoomButton`: its default is full screen on current macOS (the Eyeris report). **Not yet verified on a real window** | built |
| `ToggleAlwaysOnTop` | No public API sets another app's window level. `Supports` is false; the executor skips the step with the reason | never |
| `Center`, `SetSize`, `SnapLeftHalf`, `SnapRightHalf` | `AXPosition` / `AXSize` against `MacScreens` visible frames, with the Windows `WindowGeometry` rules ported to points | next |

## Threading and permissions

- `MacWindowSystem` and `MacWindowOperations` run on the engine worker / command executor, never on the hook thread: every AX call is IPC to the target app (bounded by the 0.5 s messaging timeout), **except when the target is Augram itself**: then AppKit answers in-process on the calling thread, so it must be the main thread (`Interop/MainThread`). That covers Augram's own elements (`Ax.IsOwn`) and the system-wide focus query, which reaches Augram whenever Augram is in front. `MacScreens` reads `NSScreen` from that thread inside an autorelease pool, and `MacClipboard` clears `NSPasteboard` there the same way; `MacOverlayWindowStyle` runs on the UI thread, as AppKit requires.
- Everything that acts (the hook's event tap, injected input, AX calls) needs **Accessibility** (System Settings › Privacy & Security › Accessibility). macOS grants it to the responsible app: Augram once it is an app bundle; during development the terminal or editor that launched it (VS Code here, already granted). Without it the hook fails to install and every window operation fails with "Accessibility permission missing".
- On macOS SharpHook's `KeyTypedEnabled` is switched off (`Engine/Input/SharpHookInputSource`): libuiohook would otherwise resolve each key press to a character with a synchronous trip to the main thread, making every key on the machine wait for the UI thread.

## Running on a Mac

```
# once: .NET SDK per global.json into ~/.dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
~/.dotnet/dotnet build
~/.dotnet/dotnet src/Augram.App/bin/Debug/net10.0/Augram.App.dll             # engine on: hook + overlay
~/.dotnet/dotnet src/Augram.App/bin/Debug/net10.0/Augram.App.dll --no-engine # UI only
pkill -f Augram.App
```

Config and logs: `~/Library/Application Support/Augram/augram.json` and `logs/augram-yyyyMMdd.log` beside it. The live window-operations test: `AUGRAM_MAC_LIVE=1 dotnet test tests/Augram.Platform.MacOS.Tests` (opens and closes a TextEdit window).
