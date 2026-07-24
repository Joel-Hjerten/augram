# Handoff: Cross-Platform Mouse Gesture App

**Purpose of this document:** Knowledge capture from a research/feasibility conversation (July 2026). No code exists yet, no stack has been committed. This file gives an implementing agent everything learned so far: prior art, a verified portable recognition algorithm, platform integration constraints, performance architecture, and open decisions.

---

## 1. Project intent

Build a **simplified, cross-platform (Windows + macOS) mouse gesture app** inspired by StrokesPlus.net (Windows-only, abandoned by its author). The user is a long-time StrokesPlus.net power user.

**In scope (v1):**
- Any mouse button selectable as the gesture-drawing button
- Draw gesture → recognize → fire action
- Actions: keystroke shortcuts and *sequences* of shortcuts (e.g. close tab, new tab, minimize/maximize window, delete text, arbitrary key chains)
- System volume up/down (and similar media controls)
- Visible gesture trail drawn over other applications

**Explicitly OUT of scope** (this is what makes it "simplified" vs StrokesPlus.net):
- No embedded scripting engine (SP.net embeds ClearScript + Chrome V8 — a huge maintenance surface)
- No text expansion, no "floaters", no window-automation API surface, no plugin system

**Non-negotiable quality bars (user's words):**
1. Gesture recognition quality must match StrokesPlus.net ("quite high quality... which this program actually achieves")
2. Zero perceptible latency while drawing
3. Trail must draw on top of essentially all applications

---

## 2. Prior art and source material

| Project | What it is | Source available? | License |
|---|---|---|---|
| **StrokesPlus.net** (the app being replaced) | C#/.NET rewrite, successor app. Freeware, Windows-only. Embeds ClearScript/V8. Author abandoned it in 2024 (shut down site + Discourse forum); final binary release Jan 2025 (v0.5.8.0). | **NO — source was never published.** | Freeware (binaries only) |
| **StrokesPlus (original)** | The C++ Win32 predecessor (ended ~2016, v2.8.6.x) | **YES** — https://github.com/minyoad/StrokesPlus | **MIT** (c) 2014 Rob Larkin — free to use/modify/port with attribution |
| **StrokesPlus.net_archive** | Community preservation repo | https://github.com/ozhegov-d/StrokesPlus.net_archive — contains a `Scripts/` folder, a `StrokesPlus.net/` folder (program files), README; repo is ~99.7% HTML (archived help/documentation pages) | n/a (no source; treat as a **behavioral specification** for how the app should feel: training flow, timeouts, button handling) |

Additional lineage note: per the original repo's `LICENSE.md`, the gesture recognition code was **ported to C++ from "HighSign" by Dylan Vester** (highsign.codeplex.com — link dead, CodePlex is gone). The recognition quality the user values descends from this algorithm. The archive README also links Wayback captures of the old site/forum:
- https://web.archive.org/web/20240509182702/https://www.strokesplus.net/
- https://web.archive.org/web/20240923100130/https://forum.strokesplus.net/

---

## 3. The recognition algorithm (VERIFIED by direct source inspection)

The repo was sparse-cloned and read during research. This is the crown jewel of the handoff: the algorithm is small, self-contained, MIT-licensed, and contains **zero Windows dependencies** — it ports verbatim to any platform/language.

**Location:** `StrokesPlusHook/StrokesPlusHook.cpp`
- Math: `#pragma region Gesture Match Math`, approx. **lines 5907–6062** (~155 lines)
- Matching/execution loop: approx. **lines 6605–6660** (inside `#pragma region Gesture Match and Execute`, starting ~line 6066)

**Function inventory (all pure math on float point pairs):**
- `GetAngularGradient(p1, p2)` — `atan2` angle of a segment
- `GetAngularDelta(a1, a2)` — absolute angular difference, wrapped at π
- `GetProbabilityFromAngularDelta(d)` — maps average angular error to a 0–100 score; scale constant `31.830988618379067` ≈ 100/π
- `GetDistance`, `GetPointArrayLength` — Euclidean segment lengths / total stroke length
- `GetInterpolatedPoint`, `GetInterpolatedPointArray(points, segments)` — **resamples the raw stroke into N equal-length segments**
- `GetPointArrayAngularMargins(points)` — converts resampled points into a vector of per-segment angles

**Pipeline (what to reimplement):**
1. Capture raw cursor points while gesture button is held (full-resolution event stream).
2. On release, resample the stroke into `Precision` equal-length segments. **Default `iPrecision = 100`** (source ~line 412; runtime-adjustable via `setMatchPrecision`).
3. Compute the angle of each segment → vector of 100 angles.
4. For each stored gesture template: resample template the same way, compute per-index angular deltas, average them.
5. Convert average delta to a 0–100 probability.
6. A gesture can hold **multiple training samples**; probabilities are averaged across samples.
7. Highest-scoring gesture wins **if above `MatchProbabilityThreshold`, default 75** (source ~line 411). Otherwise: no match → replay the original click.

**Why it feels good (preserve these properties):**
- Angle-sequence comparison ⇒ automatically **scale- and position-invariant** (draw big/small/anywhere).
- Deliberately **rotation-SENSITIVE** — an up-flick and a down-flick are different gestures. ⚠️ If falling back to academic recognizers ($1 Unistroke family), note vanilla $1 normalizes rotation away — do **not** adopt that behavior.
- Multi-sample training per gesture gives the "it learned how *I* draw" calibration feel. Keep this in the training UX.

**Template storage in the original:** XML property tree, nodes `PointPatterns/PointPattern/Point` — i.e. templates are stored as raw point lists and resampled at match time. Any serialization format works; store raw points, not precomputed angles, so `Precision` stays adjustable.

**Legal:** MIT — port directly, keep the copyright/permission notice, credit Rob Larkin and (per the original's own credits) Dylan Vester/HighSign.

---

## 4. Reference codebase map (minyoad/StrokesPlus)

Old-school monolith. **Do NOT attempt to build it** (wants Visual Studio 2010, vendors Boost 1.50, Lua 5.2.1, Scintilla; the README's build steps involve a removed signing certificate). Treat it as a reference to mine.

- `StrokesPlus/StrokesPlus.cpp` (~944 lines) — thin shell .exe
- `StrokesPlusHook/StrokesPlusHook.cpp` (**~17,164 lines**) — everything lives here. Useful `#pragma region` markers:
  - `Mixer` (~line 799) — **Windows volume control reference** (relevant to the volume feature)
  - `Graphic Functions` (~1153) — trail drawing (GDI+, `InterpolationModeHighQuality`)
  - `Action Functions` (~2136)
  - `Lua` (~5665) — ignore (scripting is out of scope)
  - `Gesture Match Math` (~5907) — **the part to port**
  - `Gesture Match and Execute` (~6066) — matching loop, click-replay, per-app action resolution
- `StrokesPlusHook/StrokesPlusHook.h` (~658 lines)
- Hook architecture: low-level mouse hook captures/suppresses events; also a useful reference for modifier handling (rocker gestures, wheel-during-gesture, Ctrl/Alt/Shift state — all tracked as booleans, see `clearCaptureVars()`).

---

## 5. Core interaction loop (the quality-critical part)

```
button down (configured gesture button)
  → suppress the event (do not deliver to the app under cursor)
  → collect points; draw trail on overlay
button up
  → if recognized above threshold: execute mapped action
  → if NOT recognized (or below movement threshold): synthesize/replay the
    original click at the original location so normal clicks still work
    (context menus on right-click, etc.)
```

The **suppress-then-replay** logic is where "feels as good as StrokesPlus" is won or lost — more than the recognition math. Edge cases: click-without-movement passthrough, drag detection, timeout handling, double-click preservation.

---

## 6. Platform integration notes

### Windows
- `WH_MOUSE_LL` low-level mouse hook: capture and suppress any button (incl. X1/X2/middle). `SendInput` for keystroke/click synthesis.
- Window min/max: `Win+Up/Down` synthesis or `ShowWindow` API directly (API route is more robust).
- Volume: synthesize `VK_VOLUME_UP` / `VK_VOLUME_DOWN` / media keys.
- ⚠️ Known ceiling: hooks cannot reach **elevated (admin) processes** unless the app itself runs elevated; the original required a signed install under Program Files for UAC-era system surfaces. Same ceiling exists in SP.net — user has likely never noticed it. Document, don't over-engineer.

### macOS
- `CGEventTap` (an **active** tap): can observe AND consume any mouse button system-wide (`otherMouseDown` for extra buttons), then re-post the click via `CGEventPost` on non-gesture.
- **Permissions:** Accessibility (and possibly Input Monitoring) must be granted; app should detect/prompt (`AXIsProcessTrusted`).
- **Distribution:** Developer ID signing + notarization effectively required for anything shared beyond the dev machine.
- ⚠️ Tap callbacks must return fast — the OS disables slow taps (`kCGEventTapDisabledByTimeout`; listen for it and re-enable). This conveniently forces the correct lean-hot-path architecture.
- Secure input fields (password boxes) block event synthesis; exclusive-fullscreen games are awkward on both OSes. Same "works in ~98% of places" ceiling as Windows.
- **"Maximize" is ambiguous on macOS** — zoom (green button), true fullscreen, and window tiling are three different things. Make it a per-action design choice; minimize is `Cmd+M`; consider the AX API for direct window ops.
- Volume: media key events (`NX_KEYTYPE_SOUND_UP/DOWN`) or CoreAudio.

---

## 7. Performance architecture (replicate this shape)

Key insight from research: **StrokesPlus.net is a managed C#/.NET app yet feels native-instant** — performance is an architecture property, not a language property. The shape to copy:

1. **Near-zero work in the event callback** — append a point, decide forward/consume, return. Nothing else.
2. **Trail on a transparent, click-through overlay window** (per-platform native window; batch points per frame rather than repaint per event — modern mice emit 1000+ events/sec).
3. **Recognition only on button-up** — matching a 100-segment template set is microseconds.
4. **Avoid web-shell overlays** (Electron-style) for the trail; jank risk. A Tauri/web *settings* UI is fine — the hot path is what matters.

---

## 8. Candidate stacks (open decision — user has NOT chosen)

**Option A (closest in spirit to SP.net, single codebase):**
C# / .NET 8+ · **SharpHook** (wraps `libuiohook`: cross-platform global hooks *and* input simulation) · **Avalonia** for the settings UI · native interop only for the overlay window and OS-specific actions (volume, window management).

**Option B (lean):** Rust core (`rdev` for global listening, `enigo` for synthesis, or direct platform APIs) + minimal UI.

**Option C:** Two thin native apps (Win32/C# and Swift) sharing only the recognition module + gesture/config file format.

In all options: port the MIT recognition code (Section 3) rather than inventing a new recognizer.

---

## 9. Suggested build order (for the implementing agent)

1. **Spike the risk first:** per-platform prototype of *capture any button → suppress → replay click on release*. This is the only genuinely uncertain part.
2. Port the recognition math + unit tests using recorded real strokes (record from the prototype). Verify scale/position invariance and rotation sensitivity.
3. Transparent overlay + trail rendering (batched).
4. Action executor: keystroke sequences, volume. Config format + gesture training UI (multi-sample per gesture).
5. macOS permission onboarding flow; signing/notarization last.

Windows-first is the well-lit path; macOS roughly doubles integration work but nothing in scope hits a wall there.

---

## 10. Naming (undecided — user's app-family theme)

The user's existing apps: **Chimera** (Spine skeleton read/author/animate/export tool) and **Eyeris** (image viewer/capture/gesture-annotation; "Eye" replacing "I" in Iris). Theme: **Greek mythological figure + embedded functional pun.**

Shortlist discussed:
- **Chiron** — Greek hybrid beast (pairs with Chimera); name derives from *kheir* = hand; *chiromancy* = divination by reading hands. (Distant collision: Bugatti Chiron.) *Conversation favorite.*
- **Mousa** — singular of *Mousai*, the Muses; the Greek word literally contains "Mous(e)". ⚠️ Plural **Mousai is taken** (a known Linux song-recognition app). Verify "Mousa" before committing.
- **Glyphon** — Gryphon respelled with *glyph* (a gesture is a drawn glyph). ⚠️ Partial collisions: a Rust text-rendering crate `glyphon`, glyphon.ai (AI platform), an old Android glyph-tracing app.
- **Augur** — Roman diviner who read meaning from flight trajectories; exactly what a recognizer does with the cursor.
- **Sigil** — a drawn symbol that invokes an effect. ⚠️ Collision: the well-known EPUB editor.
- **Mudra** — Sanskrit sacred hand gesture; "meaningful gesture" in one word (steps outside the Greek set).
- Taken in this category: **Mousai**, **Sleipnir** (Japanese mouse-gesture browser).

---

## 11. Open decisions summary

- [ ] Stack (A/B/C above)
- [ ] App name
- [ ] Windows-first vs simultaneous cross-platform
- [ ] Gesture/config file format (recommend: store raw template points, JSON or similar)
- [ ] macOS "maximize" semantics (zoom vs fullscreen vs tiling)
- [ ] Distribution ambitions (personal use vs public release — drives signing/notarization effort)

## 12. Key links

- Original StrokesPlus source (MIT): https://github.com/minyoad/StrokesPlus
- SP.net archive (behavioral spec, docs, scripts): https://github.com/ozhegov-d/StrokesPlus.net_archive
- Old site (final binaries still listed): https://www.strokesplus.net/
