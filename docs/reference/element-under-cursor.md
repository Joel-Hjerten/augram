# What is under the cursor inside a browser (research, not planned)

Status: **idea, not scheduled** (Joel, 2026-10-11: "not in dire need", thought about on and off). Nothing here is built or tested. Read this before planning any condition finer than "the window under the gesture start point".

## The wish

One gesture, two meanings, decided by where it was drawn:
- drawn over a playing YouTube video in Chrome → the player's fullscreen (`f`);
- drawn anywhere else in Chrome → maximize the Chrome window (toolbar, address bar and bookmarks stay).

Joel already has both as separate commands, so this is a convenience, not a gap. **Constraint (Joel): no noticeable cost to the page or to interaction speed.** A browser extension is ruled out as too heavy-handed (see "Rejected" below).

## What Augram knows today

The target is the window under the gesture start point ([requirements.md](../requirements.md) "the target is the window under the gesture start point"; Windows uses `WindowFromPoint`). That says "Chrome" and gives the title (e.g. "… - YouTube"), nothing about what is inside the page. A title rule plus `f` gets you "fullscreen anywhere on a YouTube page", not "only over the video".

## The approach: ask the accessibility tree

Both OSes let one process ask another for the UI element at a screen point. Chrome answers for web content once its renderer accessibility is on.

**Windows (UI Automation).** `IUIAutomation.ElementFromPoint(startPoint)`, then walk up the parents (`TreeWalker`) a bounded number of steps looking for the player:
- the `<video>` element (W3C HTML-AAM: exposed with computed role `html-video`; the UIA control type Chrome gives it needs checking), or
- YouTube's player container, `id="movie_player"`, `class="html5-video-player"`. Whether Chrome sets UIA `AutomationId` from the HTML `id` and exposes `class` as `ClassName` is **believed, not verified**.

Since Chrome 138, Chromium on Windows speaks UIA natively (no MSAA/IA2 proxy), which its team says is faster and more reliable. Chrome turns on renderer accessibility when a UIA client asks; AutoHotkey's UIA library does it explicitly by sending `WM_GETOBJECT` to `Chrome_RenderWidgetHostHWND1`.

**macOS (AX API).** `AXUIElementCopyElementAtPosition(systemWide, x, y)`, then `kAXParentAttribute` upward, checking `AXRole` / `AXSubrole` / `AXDOMIdentifier` / `AXDOMClassList`. Chrome builds its web tree only when asked: set `AXManualAccessibility = true` on the app element (the documented Electron/Chromium switch; a running screen reader overrides it). Needs the Accessibility permission Augram already holds. Which role a `<video>` gets on the Mac is **unverified**.

### Where it would sit in Augram

- Runs **at button-up on the worker**, after recognition, only when the matched gesture has a command that carries this condition **and** the window under the start point is a browser. Never in the hook (invariant 1); never during capture (invariant 4).
- A hard **timeout** (say 50 ms). On timeout or any failure the condition is false and the window-level command runs, so a slow page degrades to today's behaviour, never to a stall.
- Probably a new app-group condition ("pointer over element matching …") rather than a step, so the existing "which app group wins" rules pick between the YouTube-player group and the Chrome group. Shape to decide in a plan.
- In YouTube fullscreen the video covers the screen, so the same gesture over it finds the player again and can send `f` (or `Esc`) to leave.

## The cost question (Joel's constraint)

This is what decides it, and nothing found online measures it for our case:
- **Turning accessibility on is not free.** Electron's docs warn that rendering the accessibility tree "can significantly affect the performance of your app" and should not be on by default. Linux Chrome once had to stop auto-enabling it because users without assistive tech paid the cost for nothing (2020).
- **The first query can be slow**: one UIA user reports more than 300 ms on a complex Chrome page while the tree is built. Later point queries should be much cheaper, but YouTube pages are large (comments, recommendations).
- **Once on, it may stay on** for that Chrome session, so the page carries the tree (memory, DOM-mutation bookkeeping) even between gestures. Whether current Chrome switches it back off after a while without queries is unverified.

So "no noticeable cost" is plausible but unproven.

## What to measure before any plan

A throwaway console probe (not committed; no hooks, no overlay, so safe to run) on each OS, on a long YouTube watch page:
1. Time of the first `ElementFromPoint` / `AXUIElementCopyElementAtPosition` over the video (cold), and of the next 20 (warm).
2. What the parent chain looks like: control types / roles, `AutomationId` / `AXDOMIdentifier`, class names. Is `movie_player` or the `<video>` reachable within ~10 steps?
3. Chrome's CPU and memory before and after (Task Manager / Activity Monitor), scrolling the comments and playing video with accessibility on. Is there any felt difference?
4. Does it keep working in YouTube fullscreen and in theater mode, and after an SPA navigation to another video?
5. Same on the Mac with `AXManualAccessibility`.

Tools to look without code: Accessibility Insights for Windows (shows UIA control type and AutomationId per element); Accessibility Inspector on the Mac.

If 1 and 3 come back small, it's worth a plan. If the cold cost is high, the condition could start accessibility only for windows whose title already says YouTube, so other pages never pay.

## Rejected

- **Browser extension + native messaging** (a content script reports "mouse over `<video>`"). The most precise option, and works on any site, but means building, shipping and updating an extension per browser. Joel: too heavy-handed.
- **Window-title rule alone**: already possible today; doesn't distinguish video from the rest of the page.
- **Replaying a double-click on the video** (YouTube toggles fullscreen on double-click): off the video it selects text instead, and it needs the same "is it the video?" answer anyway.

## Sources

- Chrome for Developers, [Native UI Automation for Windows in Chromium](https://developer.chrome.com/blog/windows-uia-support-update) (UIA native by default from Chrome 138).
- Descolada's AutoHotkey UIAutomation wiki, [Common pitfalls](https://github.com/Descolada/UIAutomation/wiki/08.-Common-pitfalls;-tips-and-tricks) and [UIA tree](https://github.com/Descolada/UIAutomation/wiki/02.-UIA-tree) (activating Chromium accessibility from a client).
- Electron, [Accessibility tutorial](https://www.electronjs.org/docs/tutorial/accessibility) (`AXManualAccessibility`, the performance warning).
- orca-list, [--force-renderer-accessibility needed again](https://lists.gnome.org/archives/orca-list/2020-May/msg00298.html) (auto-enable false positives cost users performance).
- W3C, [HTML Accessibility API Mappings](https://www.w3.org/TR/html-aam-1.0/) (`<video>` → `html-video`).
