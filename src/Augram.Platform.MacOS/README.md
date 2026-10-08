# Augram.Platform.MacOS

macOS implementations of the same Core ports as `Augram.Platform.Windows`, through CoreGraphics, the Accessibility API (`AXUIElement`) and a handful of AppKit calls via the Objective-C runtime. Started 2026-10-07 on Joel's Mac (Apple silicon, built-in Retina display at 2x plus an external display at 1x, "Displays have separate Spaces" on).

**May reference:** `Augram.Core` only. AppKit / Accessibility interop goes here and nowhere else.

**Must never contain:** Avalonia, SharpHook, business rules, or Windows types.

## What is here

| Folder / file | Port | What it does |
|---|---|---|
| `Interop/MacNative` | | The only P/Invoke surface: CoreFoundation, CoreGraphics, HIServices (Accessibility), libobjc, libproc. `LibraryImport`, blittable types, bools as bytes. |
| `Interop/Cf`, `Interop/Ax`, `Interop/ObjC` | | Wrappers: constant `CFString`s, reading CF values, AX attribute get/set/press with a 0.5 s messaging timeout per app, `AXError` → log reason, `NSRect` returns (arm64 vs x86-64), autorelease pools. |
| `WindowSystem/MacWindowSystem` | `IWindowSystem` | `WindowAt` hit-tests the window server's list (`CGWindowListCopyWindowInfo`, front to back; no app is asked). `Foreground` is the focused window of the focused app (AX). `Activate` applies A20, then raises the window and sets the app `AXFrontmost` (works from a background app; `NSRunningApplication.activate` does not since macOS 14). |
| `WindowSystem/MacWindowOperations` | `IWindowOperations` | Close, Minimize, MaximizeOrRestore. The rest report not supported until built (table below). |
| `WindowSystem/MacWindowPick`, `MacRect`, `MacMaximize` | | Pure rules, tested on every OS: which window a point hits, desktop and full-screen flags, the Cocoa ↔ top-left flip, what counts as filled, which screen a window is on. |
| `WindowSystem/MacWindowList`, `MacScreens` | | Native readers: window list, display bounds, process path (`proc_pidpath`); `NSScreen` frames and visible frames flipped to top-left points. |
| `Overlay/MacOverlayWindowStyle` | `IOverlayWindowStyle` | `ignoresMouseEvents = YES`, read back (invariant 6); status-window level (over the Dock and menu bar); all Spaces and full-screen apps; out of the Cmd-` cycle. `Place` sets the frame in points. |
| `Input/MacCursorProbe` | `ICursorProbe` | `CGEventGetLocation` of a blank event. |
| `Display/MacDisplayModes` | `IDisplayModes` | The Display steps (learnings 0002 §3; **compiled on Windows, not yet run on a Mac**). Lists the active displays (`CGGetActiveDisplayList`, `CGDisplayBounds` in global points, `CGMainDisplayID`, `CGDisplayIsBuiltin`) with every mode of `CGDisplayCopyAllDisplayModes` including the duplicate low-resolution ones. A resolution is points (the "looks like" size), a rate CoreGraphics' own rounded to three decimals (0 = not reported). `SetMode` finds the same `CGDisplayMode` again, prefers the HiDPI twin (`MacModePick`, pure and tested on every OS) and applies it in one `CGBeginDisplayConfiguration` transaction completed `kCGConfigurePermanently` (like System Settings), cancelling on error. HDR: no public API, so `CanSwitchHdr` is false and every display reads unsupported. CoreGraphics only, no AppKit, so it runs on the calling thread (executor, or the UI thread for the step form); the Mac session should confirm a mode change from the executor thread is fine and that the modes listed match System Settings' "Show all resolutions". |
| `Display/MacDisplayList`, `MacNativeMode`, `MacDisplayReading` | | Native reader and applier (`Interop/MacNative.Display.cs`), and the plain records it returns. |
| `MacAccessibility` | | `IsTrusted()`; the App logs it once at engine start. |

Not built yet: `ISystemEvents` (sleep/wake via `NSWorkspace` notifications), `IStartupRegistration` (`SMAppService`, needs an app bundle), the placement operations and an app bundle.

## Coordinates and identity

- **Units are points, origin top-left of the main display, y down** everywhere this project hands a value out: the hook reports the pointer that way, CoreGraphics window bounds and the Accessibility API use it, and so does Avalonia for screens and window positions. Only `NSScreen` is bottom-left based; `MacRect.FromCocoa` flips it by the main screen's height. `WindowSize` is read as points.
- `WindowIdentity.Handle` and `RootHandle` are both the `CGWindowID` (a macOS window has no child windows to resolve). `ProcessName` is the executable's file name (`Safari`, `Google Chrome`, `Code`), the counterpart of `chrome.exe`; `ProcessPath` is the full path inside the bundle; `ClassChain` is empty; `Title` is null unless the process may record the screen (Screen Recording permission).
- `IsDesktop`: the hit window is below the normal layer (wallpaper, Finder's desktop icons). Window operations refuse the desktop. A smaller window on a higher layer (menu bar, status items) wins over the window behind it, so a gesture there never reaches a window the user was not pointing at. Display-sized windows above the normal layer are skipped: the Dock draws itself in a transparent window as large as its display, and overlay utilities do the same, so they would otherwise swallow every point on that display.
- A window operation finds the window again by its `CGWindowID` among its app's `AXWindows` (`_AXUIElementGetWindow`, private but what every macOS window manager uses); not found → "window gone".

## Window operations

| `WindowOperation` | macOS | State |
|---|---|---|
| `Close` | `AXPress` on the window's `AXCloseButton`: the app runs its own close path ("save changes?"); the process is never killed | built |
| `Minimize` | `AXMinimized` = true | built |
| `MaximizeOrRestore` | **Fill**: set `AXPosition` + `AXSize` to the visible frame of the window's screen (menu bar and Dock excluded), remembering the frame from before; when the window already fills it, put the remembered frame back, or press `AXZoomButton` if Augram did not fill it. A window in native full screen leaves it (`AXFullScreen` = false). D6 working choice (2026-10-07), Joel to confirm: zoom is defined per app and often fits content, full screen takes a Space | built |
| `ToggleAlwaysOnTop` | No public API sets another app's window level. `Supports` is false; the executor skips the step with the reason | never |
| `Center`, `SetSize`, `SnapLeftHalf`, `SnapRightHalf` | `AXPosition` / `AXSize` against `MacScreens` visible frames, with the Windows `WindowGeometry` rules ported to points | next |

## Threading and permissions

- `MacWindowSystem` and `MacWindowOperations` run on the engine worker / command executor, never on the hook thread: every AX call is IPC to the target app (bounded by the 0.5 s messaging timeout). `MacScreens` reads `NSScreen` from that thread inside an autorelease pool; `MacOverlayWindowStyle` runs on the UI thread, as AppKit requires.
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
