# StrokesPlus.net config: schema + Joel's real usage profile

**Purpose:** Joel's live StrokesPlus.net configuration is the best available specification of what Augram must actually do. This doc records (a) where the data lives, (b) the parts of the SP.net schema Augram cares about, (c) what ~5 years of accumulated config actually uses, ranked by evidence, and (d) the requirement implications. Analysed 2026-10-05 against the live file written 2026-09-22.

Read this before: deciding D4 (timeouts/modifiers), fleshing out F5 (actions) and per-app matching, designing an importer, or building a recognizer test corpus.

## 1. Where the data lives

| What | Where | Usable? |
|---|---|---|
| **Live config (source of truth)** | `%APPDATA%\StrokesPlus.net\StrokesPlus.net.json` (~700 KB, UTF-8 with BOM) | **Yes, plain JSON.** Strip the BOM, then any JSON reader works. |
| Backups | `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\StrokesPlus.net\Settings\*.spexport` (2022 → 2026) | **No.** `.spexport` is .NET `BinaryFormatter` output of `StrokesPlus.net.Code.ImportExport`; it needs SP.net's own assemblies to deserialize. The live JSON supersedes them. |
| Portable install 0.5.5.3 (same folder) | `Default_StrokesPlus.net.json` = factory defaults; `HTML/` = the settings UI (jQuery + Bootstrap in WebView2) | Defaults are useful for diffing what Joel changed. `HTML/js/strokesplus-net-gesturelist.js` shows how SP.net draws gesture icons (§6). |
| **Official archive site** | https://strokesplus.net (= GitHub Pages repo `roblarky/roblarky.github.io`): final 0.5.8.0 and classic 2.8.6.4 installers, `ChangeLog.txt` (412 KB, every version), `data.json` / `cdata.json` (the SP.net and classic forums archived as JSON: Category, Forum, Topic, Message, author, date; ~3700 posts), `index.html` is a client-side forum search | **Yes.** Clone the repo (58 MB) and search the JSON with node. Behavioural findings mined from it are in §9. |
| Other | https://github.com/xkonglong/strokesplus.net (Chinese preservation README + 4 screenshots; shows one app group matching several browsers via regex alternation); gist `Kambaa/15047e4…` (classic S+ `acSendKeys` key-syntax reference) | Minor; used for the importer's SendKeys parsing notes. |

## 2. Schema (only what Augram needs)

Top level is one flat settings object (~280 keys; most are scripting / floater / text-expansion noise). Relevant members:

```
Settings
├─ StrokeButton, SecondaryStrokeButton   int, WinForms MouseButtons enum:
│     Left 0x100000 · Right 0x200000 · Middle 0x400000 · X1 0x800000 · X2 0x1000000 · 0 = none
├─ MatchProbabilityThreshold (75), MatchPrecision (100)
├─ MinGestureStartDistance, MinSegmentLength, CancelDelay, ResetCancelDelayOnMovement,
│  DisableRelayOnGestureTimeout, RelayGestureOnNoMatch, PlaySoundOnNoMatch
├─ CaptureModifiersOnMouseDown, RockerSupport, FireOnMouseWheel
├─ AlwaysActivateWindowOnGestureComplete, ActivateWindowUseRootParent
├─ DrawGesture, PenWidth, PenOpacity, PenColor{R,G,B}, SecondaryPenColor, MaxDrawPoints
├─ Gestures[]             Gesture
├─ GlobalApplication      Application (Description null)
├─ Applications[]         Application
├─ IgnoredApplications[]  IgnoredApplication
└─ Hotkeys[], <Event>Action (LeftClickAction …)   unused by Joel

Gesture { Name, Active, MatchCount, PointPatterns[ { Order, Points[ {X,Y} ] } ], MultiPointPatterns (null) }
  one PointPattern per training sample; raw integer points; stock gestures are 2–3 points, e.g. Up = (0,500)→(0,100)

Application {
  Description, Active, MatchCount, Categories[], NoGlobalActions, CancelDelay (-1 = inherit),
  matchers, each { Value, IsRegex }: FileName, FilePath, OwnerClassName, RootClassName, ParentClassName,
    ControlClassName, OwnerWindowText, RootWindowText, ParentWindowText, ControlWindowText; ControlID
  Actions[] Action
}

Action {
  Description, Category, Active, MatchCount,
  GestureName ("" = no gesture; the trigger is then a modifier or wheel tick),
  modifiers (bool): Control Alt Shift Left Middle Right X1 X2 WheelUp WheelDown,
  UseSecondaryStrokeButton, Capture,
  Steps[ { Method, MethodParameters[ {Name, Value} ] } ],   the no-code "step" editor
  Script                                                   JavaScript (ClearScript/V8); may coexist with Steps
}

IgnoredApplication { Description, Active, DisableOnFocus, same matchers as Application }
```

`Type` on an Action does **not** distinguish Steps from Script (0 and 1 both appear with either). Treat an action as Steps when `Steps` is non-empty, else Script.

## 3. Joel's settings that matter (vs factory default)

| Setting | Joel | Default | Meaning for Augram |
|---|---|---|---|
| StrokeButton | `0x400000` = **Middle** | Right | Gesture button is the middle button (wheel click). Secondary button: none. |
| MinGestureStartDistance | **30 px** | 6 | Press + less than 30 px of movement = plain click, replayed. This is the click-passthrough threshold (the spike used 10). |
| CancelDelay / ResetCancelDelayOnMovement | 1000 ms / true | same | Hold still for 1 s mid-gesture and the gesture is cancelled. |
| DisableRelayOnGestureTimeout | **true** | false | A cancelled gesture does **not** replay the click. |
| RelayGestureOnNoMatch | **false** | true | A stroke that matches nothing does **nothing** (no middle-click replay). Essential with Middle as the stroke button: a stray middle click opens links in new tabs or starts autoscroll. |
| PlaySoundOnNoMatch | false | true | Silent on no match. |
| MatchProbabilityThreshold / MatchPrecision | 75 / 100 | same | Confirms handoff §3 defaults. |
| MinSegmentLength | 6 | 6 | Minimum px between stored points (input decimation). |
| FireOnMouseWheel | true | true | Wheel while holding fires actions (§5). |
| CaptureModifiersOnMouseDown / RockerSupport | true / true | same | Enabled, but barely used (§5). |
| AlwaysActivateWindowOnGestureComplete / ActivateWindowUseRootParent | true / true | same | The window under the gesture **start point** is activated before the action fires. Actions target where you drew, not what had focus. |
| PenWidth / PenOpacity / PenColor | 5 / 0.5 / (0,255,64) green | 5 / 0.5 / (56,169,255) | The trail look Joel is used to. |
| ShowGestureHints | false | true | No live hint popup while drawing. |

## 4. Usage profile (from `MatchCount`, which SP.net increments on every fire)

- **90 gestures** defined; 24 unused; only **3 have more than one training sample** (`/ Up / Down`, `e`, `h`). Two action references point at gestures that no longer exist (`+ Right Down`, `Eraser`): an importer must warn, not crash.
- **264 actions, 212 active**, across Global + **19 apps** + **6 ignored apps**. The 41 inactive globals are mostly SP.net's shipped examples.
- Fires: Global 256 k · Chrome 261 k · PotPlayer 69 k · Explorer 13 k · Tekken 8 11 k.

Top gestures by all-time fires: `\Up` 169 k · `↔ Left` 80 k · `↕ Down` 75 k · `/Up` 57 k · `↕ Up` 32 k · `/Down` 25 k · `↔ Right` 13 k · `\Down` 10 k · `\Up \Down` 7.6 k · `Left Up` 5.3 k. **Single flicks in 8 directions dominate**; two-segment L-shapes and out-and-back strokes are the next tier; letters (C, S, T, M, V, W, J, e, h, x) and loops are the long tail.

Step vocabulary across active actions:

| Step | Uses | Step | Uses |
|---|---|---|---|
| SendHotKey (modifiers + virtual key) | 175 | CloseWindow | 4 |
| Delay | 24 | MouseClick at the gesture start point | 3 |
| SendKeys (type a string) | 18 | SendAltDown / SendAltUp | 3 / 3 |
| SendVKey (bare virtual key; used for media keys) | 14 | MinimizeWindow | 2 |
| ConsumePhysicalInput | 2 | MaximizeOrRestoreWindow, ToggleWindowAlwaysOnTop, SetWindowSize, Center, Run, OpenSettings, TogglePlaybackMute, SendWinDown/Up | 1 each |

Trigger kinds across active actions: **201 gesture-only**, 5 wheel-up, 5 wheel-down, 1 Shift chord. No rocker, no X1/X2 chords, no secondary stroke button in use (the two global "Zoom" wheel actions require the secondary button, which is set to none, so they are dead).

Script actions (18 active) reduce to four things: run a program with arguments (refresh-rate switcher, `taskkill /f` elevated), snap the window to the left or right half of the work area via the window rectangle, post `WM_MOUSEWHEEL` with `MK_CONTROL` to the window under the start point (Ctrl+wheel zoom in Explorer), and clear the clipboard. Hotkeys, text expansion, floaters, regions, plug-ins: unused.

## 5. What Augram must support, ranked by evidence

1. **8-direction flicks and multi-segment strokes, rotation-sensitive.** `\Up` ≠ `\Down`; `↕ Up Down` ≠ `↕ Down Up`. Handoff §3 algorithm as-is.
2. **Per-app overrides of the same gesture.** `\Up` = close window globally, close tab in Chrome/Firefox/Explorer/Sublime, "do nothing" in Steam games. The **global → app override → explicit "override to nothing"** model is in daily use (Windows Desktop ignores Close/Min/Max/Center; Steam games ignore Close). One app (FF7 Rebirth) uses `NoGlobalActions` to shadow all globals.
3. **Wheel while holding the stroke button.** Volume Up/Down globally (**36 k + 37 k fires**, the second most used feature in the whole config), Ctrl+wheel zoom in Explorer (11 k), volume in Tekken. Must be v1; it is part of D4.
4. **Target = window under the gesture start point, activated first.** Chrome actions land on the Chrome window you drew over, not the focused window.
5. **Keystroke sequences with delays** (Alt+Tab in games: Delay, Tab, Delay, AltUp, AltDown; Save Image: right-click at start point, V, 700 ms, Enter), **typed text** (game console: backtick, `fov 67.5`, Enter), and **bare virtual keys** for media (play/pause, next/prev, volume, browser back/refresh).
6. **Window actions:** close, minimize, maximize/restore, always-on-top toggle, center, set size, snap to left/right half of the work area (unmaximize first).
7. **Run program with arguments** (plus elevated and hidden flags): 6 active actions.
8. **App matching:** process file name exact (14) or regex (`PotPlayerMini.*.exe`, `Spine(?:-1)?\.exe`), full path regex (`^C:\Program Files (x86)\Steam\steamapps\common\.+$` = "all Steam games"), owner window title (`Chimera`, an Electron app), and a window-class chain (`Progman|WorkerW` + `SHELLDLL_DefView` + `SysListView32` = the desktop). Class-chain matching is Windows-specific and belongs behind the platform adapter; the others port to macOS as bundle id / path / title.
9. **Ignore list with two modes:** "gestures don't fire over this app" (Blender, Resolve, a game) vs "disable Augram entirely while this app is focused" (`DisableOnFocus`, VMware, so the guest VM gets the raw button).
10. **Click-passthrough rules:** under 30 px = replay click; moved but unrecognized = do nothing (configurable); held still for 1 s = cancel without replay.
11. **Categories** for grouping actions per app (Photoshop groups blend modes) and **usage counters** on gestures, actions, and apps. Cheap to keep and useful for sorting the workbench.

Low priority or not needed in v1, by evidence: rocker gestures (all rocker actions inactive), modifier+button chords (one use, and it is a passthrough hack: "if Shift is held, send a real Shift+right-click"), secondary stroke button, multi-sample training UX polish (3 of 90), gesture hints, no-match sound, open URL / paste text (no uses).

## 6. How SP.net renders gesture icons (confirms F4)

`strokesplus-net-gesturelist.js`, `DrawToCanvas`: normalize the template's points into the canvas (60% of the box, centered), stroke the polyline with a white-outlined pen, draw a filled triangular arrow tip along the last segment's direction, and render inactive items at 50% opacity on a grey background. Nothing is hand-drawn. Augram's F4 should do the same from the raw point list.

## 7. Implications for requirements (PROPOSED, Joel to confirm)

- **F1 / D4:** adopt SP.net's three thresholds as user-tunable options with Joel's values as defaults: start distance 30 px, cancel delay 1000 ms (reset on movement, no replay on cancel), no replay on no-match. Wheel-during-gesture is v1. Rocker and modifier chords: defer.
- **F5:** the step vocabulary in §4 is the v1 action set. Add explicit "override to nothing" and per-app "suppress globals". Add "run program". Make "target window = under gesture start, activate first" an invariant.
- **F5 app matching:** process name / path (regex-capable) / window title, plus a platform-specific escape hatch (Windows class chain).
- **F6:** default trail = width 5, 50% opacity, Joel's green.
- **F8 / new D9:** **import from `StrokesPlus.net.json`** should be a v1 feature. It migrates 90 gestures, 19 apps, and 212 actions in one step and is the fastest path to Augram replacing SP.net on Joel's machine. Script actions that do not map to a step get imported disabled, with the script kept as a comment.
- **Recognizer tests:** Joel's 90 templates are a real-world fixture set for the port (scale/position invariance, rotation sensitivity, and a "which of 90 does this stroke match" regression).

## 8. Settings UI structure (from screenshots, 2026-10-05)

Screenshots live in [strokes-plus-net-screenshots/](strokes-plus-net-screenshots/). This is the app Joel has used daily for years; its structure is the baseline D2 starts from, minus everything out of scope.

**Shell.** A top toolbar of large icon+label buttons selects the section: Global Actions · Applications · Floaters · Gestures · Ignore List · Hot Keys · Macros · Plug-Ins · Options, with Import/Export · Console · Help · About right-aligned. Bottom bar: Apply · OK · Cancel (explicit apply; nothing is live). Augram keeps five sections: **Gestures · Global · Applications · Ignored · Options**, plus Import. Floaters, Hot Keys, Macros, Plug-Ins, Console go away.

**Global Actions.** Two panes. Left: a tree of collapsible **categories** → actions, each row showing the gesture glyph (green stroke with arrowhead) and the action name; inactive rows are greyed. Toolbar: Add Category, Add Action, Toggle Active, Copy, Paste, Rename, Delete, Import, Export. Right: the **action editor**:
- Header: gesture tile (glyph + name; click to pick another gesture), modifier grid (Control / Alt / Shift / Wheel Up / Wheel Down / Left / Middle / Right / X1 / X2 + "Capture Modifiers: Either"), hint text, Use Secondary Stroke Button, Active.
- Tabs **Steps / Script**. Steps: a list of step rows (e.g. "Send Keys", "Send Hot Key") with Insert / up / down / copy / delete, and a **step editor** beside it: notes, Active, command picker (category dropdown + command dropdown with one-line description), then a **parameter form** specific to the command (e.g. "Press Key(s) Below" is a hotkey-capture field showing `LMENU+TAB`; Send Keys shows a text box). Script tab is a code editor; out of scope for Augram.

**Applications.** Three panes: app list (left, plain bold names) · the same action tree as Global (middle) · the same action editor (right). Sub-tabs per app: Action List · Text Expansion · Before/After Automation · Exclusion Zones · **App Definition**. App Definition is a form: Custom Cancel Delay, Region Type, **No Global Actions**, then ten matcher rows (Owner/Root/Parent/own Title, Owner/Root/Parent/own Class, Control ID, Module Name, Module Path), each with a **crosshair "pick a window" button** and a Use Regex checkbox, then Ignore If Full Screen and Active. The crosshair is detect-to-assign applied to apps: drag it onto a window and the fields fill in. Keep that, with far fewer fields.

**Select Gesture picker** (`Capture 2026-10-05 20-26-35.png`): clicking the gesture tile in the action editor opens a modal with the same glyph grid as the Gestures page, plus **New Gesture…** (starts training from here), **No Gesture** (action has no gesture, used for wheel/modifier-triggered actions such as Joel's volume commands), OK, Cancel. The glyph grid is therefore one reusable component with two hosts.

**Gestures.** A flat grid of tiles, glyph above name, grouped under an "Active" header. Toolbar: **Add or Train Gesture**, Toggle Active, View All Patterns, Rename, Delete. The glyph is the only visual identity a gesture has, which is why F4 matters.

**Gesture Training dialog** (opened from the Gestures toolbar; the user then draws anywhere on screen): a thumbnail of the drawn stroke, an editable combo box pre-filled with the **best-matching existing gesture name**, and three lines of copy: "This drawing was matched with the gesture above. To create a new gesture, enter a new name and Save. Otherwise Save adds this drawing to the existing gesture." Save / Cancel. This is the flow Joel wants: explicit, from the Gestures section, never ambient. It is also how multi-sample training happens (Save onto an existing name).

**Ignore List.** App list left (inactive entries carry a ⊖ marker), and the same matcher form right, plus **"Disable S+ if this App Gains Focus"** at the top. Two ignore modes in one screen.

**Options › General.** Stroke Button radio group (Left / Middle / Right / X1 / X2; Joel: Middle), Secondary Stroke Button (Joel: None), **Ignore Key** checkboxes (Control / Alt / Shift / Win; Joel has **Control** checked, so Ctrl + stroke button bypasses capture and reaches the app, a useful escape hatch), Cancel Delay 1000 ms + Reset on Movement or Modifiers, Stroke Width 5, Stroke Opacity slider, Stroke Color (green) and Secondary (red), culture, and checkboxes: Draw Gesture, Rocker Support, Always Activate Window Where Gesture Began (+ Always Use Root Window), Activate Window Under Mouse Wheel, Relay Mouse Wheel Events. Other option tabs (Hints, Touch, Text Expansion, Macros, Exclusion Zones, Script, Advanced) are out of scope.

**What this implies for Augram's component vocabulary (F7).** (see end of §8)

## 9. Behaviour mined from the SP.net forum archive and change log (2026-10-05)

Source: `data.json` / `ChangeLog.txt` in the strokesplus.net repo (§1). `[N]` = forum topic id, `x.y.z` = version. The classic C++ source ([strokesplus-classic-source.md](strokesplus-classic-source.md)) confirms most of this from the code side; where the two disagree, the source wins.

**Capture and click relay**
- The hook touches nothing until the stroke button goes down [8422]. Click vs gesture is decided on button-up: one point and no wheel/other button/modifier state → inject down+up at release; otherwise fire the gesture path [7081, matches source].
- **Stuck-button class of bugs, recurring for years:** consuming an up without its down, or deciding "click vs gesture" from accumulated modifier/button state, leaves Windows believing a button is held (0.2.4.6, 0.2.5.8, 0.2.8.3, [7081], [7238]). Rule for Augram: every consumed down must be paired with a consumed up for the same button, and stale modifier state is a reason to *relay*, never to swallow.
- A hook thread that blocks waiting on injected input hung the machine (0.3.9.1). The engine worker, never the hook thread, injects input.
- Cancel delay: classic injects the button down and hands the physical up through (hold becomes a drag); `DisableRelayOnGestureTimeout` (0.2.7.5–0.2.7.8, Joel: on) suppresses that and consumes the up. Wheel ticks abandon the cancel timer entirely.
- Relay on no match / on timeout replays "the original down, movements, and up" with 1 ms sleeps per point (0.3.4.1), used for right-drag in Explorer and browser-extension gesture tools [7105]. Joel has both off.
- Double right-click-and-drag cannot work with relay-at-release [7144]; accepted ceiling.
- Ignore Key (Joel: Control) held *before* the stroke button means the button is never captured; the key itself is never consumed. Bugs: stuck in ignore mode until a foreground change reset it (0.2.4.9). Augram re-reads modifier state at each button-down rather than tracking it.
- `SM_SWAPBUTTON` must be honoured when injecting (0.5.x) [8347].

**Recognition**
- Threshold 75 and precision 100 are the only knobs; raising the threshold "requires more precise drawing or a few more patterns" [7220]. Setting either to 0 breaks matching [c1214].
- Rob described multi-pattern scoring as "highest match probability wins" [c1055]; the classic source *averages* probabilities across a gesture's patterns. SP.net's behaviour is unknown. Augram implements both as a scoring mode and tests against Joel's three multi-pattern gestures (A7 diagnostic).
- SP.net import deduplicates gestures by matching them against each other at a forced 90%. Same idea for Augram's "likely to be confused" check and importer.

**Wheel, rocker, modifiers**
- Wheel-while-holding: first tick stops drawing and modifier capture, fires immediately with the wheel direction as trigger, every further tick fires again, cancel delay abandoned, the final up is consumed [7156, 8368]. A gesture *can* precede the wheel [c880]. Matches the source.
- "Capture Modifiers: Before / After / Either" = whether the modifier was pressed before or after the stroke button; Before is never consumed, After is. Shift+right-click only keeps working because the default config ships an action that re-sends it [8319]. Augram: deferred (F1).

**Window activation and app matching**
- "Always Activate Window Where Gesture Began" became the default because "SO MANY people complained" without it. Since 0.3.3.x it activates the **root owner under the gesture start only if it differs from the foreground root and is not the Desktop** [7083]. Activating always broke popup and find windows (Notepad++, XTranslate) and Edge's window hierarchy [7099, 7143]. This refinement goes into the activation invariant (F5).
- Foreground rights: a splash window exists so `AllowSetForegroundWindow` conditions are met; forcing `SPI_SETFOREGROUNDLOCKTIMEOUT` was made opt-in (0.2.9.4) because it is a system-wide change. Classic offered `AttachThreadInput` and the Alt-press hack. Checklist B1.
- UWP: the exe seen at the border is `ApplicationFrameHost.exe`; the point inside gives the real app [8264]. Desktop = `SysListView32` under `Progman`/`WorkerW`; Explorer's file view is `DirectUIHWND`. Full screen = window rect equals its monitor rect, desktop excluded (0.3.6.0).
- Exclusion zones (0.3.5.x) pass events through in screen or window rectangles. Not needed for v1.
- Hook window counted as "visible" and blocked exclusive fullscreen games until 0.5.6.3 [8471]. Augram's overlay must not be visible when idle.

**Trail**
- Colour-keyed GDI window, erase by retrace (classic) then by background fill (0.5.7.3, flickers on weak GPUs). Drawing inside the hook proc lagged the pointer when DWM was busy (video playing) [7081]; fixed by moving the hook to its own thread and posting points (0.3.4.0). Idle ping every 20 minutes to avoid a slow first draw (0.3.4.1). Mixed-DPI monitors produced ghost segments and phantom strokes [8362]. "Draw surface always on top" confused some apps [8315].

**Hotkeys**
- Global hotkeys via `RegisterHotKey`; combos owned by Windows (Win+R, Win+E) fail. No evidence SP.net captures Win+L itself in the hotkey field; `SendKeys("@l")` does not lock [7178]. The hotkey-field suppression Joel wants (F5) must be verified in spike B4 rather than assumed from SP.net.

**Robustness**
- Windows drops a low-level hook whose callback stalls for seconds [6]; Win11 users lose gestures after standby/lock until a manual Reload, never resolved [8413]; session lock/unlock disables and re-enables with state reset (0.2.4.9); WinEventHook re-hooked after display change (0.3.9.8). All of this is checklist B3.
- High-resolution timer sleeps "use a LOT more CPU" [8260]; ~70 MB RAM considered normal [8316]; working-set trimming removed after VirtualBox trouble (0.3.4.0).

**Recurring user asks** worth remembering: per-app stroke button; auto-disable in full-screen games but not on the desktop; hints that stop appearing; DPI scaling ugliness ("S+ runs best at 100%"); keep Shift/Ctrl + right-click working.

**Component vocabulary from §8.** The same handful of components recur everywhere and should exist exactly once: `GestureGlyph` (grid tile, tree row, editor header, training thumbnail), `ActionTree` + `ActionEditor` (identical in Global and per-app), `StepList` + per-step-type parameter forms (one per action type, supplied by the action type itself), `AppMatcherForm` with window picker (Applications and Ignored share it), `MasterDetail` layout (list left, editor right), toolbar with small icon+text buttons, section toolbar with large icon+label buttons. Open for D2: explicit Apply/OK/Cancel like SP.net, or live-save with undo.
