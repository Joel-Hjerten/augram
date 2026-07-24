# ADR-0001: App shell & language

**Status: PROPOSED** — recommendation made, awaiting Joel's decision. Do not treat as closed.

## Context

Augram is a tray-resident background utility whose hot path is OS-level: a global mouse hook that must *suppress* events, a click-through overlay for the trail, and input synthesis. Runs 24/7, so idle footprint matters. Must eventually ship on Windows + macOS; Joel currently has no Mac to test on.

The UI is a **real workbench** (Joel, 2026-07-24 — correcting an earlier draft of this ADR that undersold it): used heavily when setting up, testing, and configuring gestures. It needs gesture lists rendered as icons, sections for global/app-specific/ignored-app command scopes, tabs, checkboxes, radio groups — a proper desktop settings application. Hard constraint: **one UI implementation must serve both Windows and macOS** — no double-build.

Joel's prior app Chimera is Electron, so Electron is the familiar path ("Electron once again?").

## The hard constraint that drives everything

The suppress-then-replay loop (requirements F1) cannot be written in JavaScript. Electron has no API for global input hooks, and the npm ecosystem's standard answer, `uiohook-napi` (wraps the same libuiohook that SharpHook wraps), is **listen-only — it cannot suppress/consume events**, which is the one capability this app is built around. An Electron build would therefore need a custom C/C++ native addon per platform, or a separate native helper process doing hook + overlay + synthesis, with Electron reduced to the settings UI talking to it over IPC.

So the real choice is not "Electron vs native" — the hot path is native in every scenario. The choice is what the *shell around the native core* is.

## Options

### A. C# / .NET 8 + SharpHook + Avalonia — RECOMMENDED
Single language, single process. SharpHook (libuiohook) provides hooks **with suppression** (`SuppressEvent`, verified working in our spike) and input simulation, cross-platform. Avalonia provides the tray icon, settings UI, and windows on both OSes from one codebase. Thin platform adapters for the few OS-specific bits (overlay window tuning, volume, window management, macOS permission prompts).

- ✅ Suppression already proven here (spike, 2026-07-24); the riskiest requirement is de-risked on this stack
- ✅ One language, one process, one deployment (`dotnet publish` self-contained per platform)
- ✅ Idle footprint ~40–80 MB — acceptable for 24/7 tray residency
- ✅ StrokesPlus.net itself was C#/.NET — proof the feel is achievable here
- ➖ XAML/Avalonia is less familiar than web UI to Joel; UI iteration is likely agent-driven anyway, and Avalonia is well-documented
- ➖ Avalonia tray/overlay have occasional per-platform quirks — mitigated by keeping overlay behind a platform adapter where we can drop to native APIs

### B. Electron UI + native helper process
Chimera-style Electron app for settings; separate native daemon (C#, Rust, or C++) owns hook/overlay/actions; JSON-RPC between them.

- ✅ Web UI familiarity; could reuse Chimera UI conventions/styles
- ➖ Two runtimes, two processes, IPC protocol, two things to keep alive and update
- ➖ ~150–300 MB idle for a utility that mostly sits in the tray
- ➖ The native helper is basically Option A minus its UI — Electron is *additional* work, not alternative work

### C. Rust core + Tauri or minimal native UI
- ✅ Smallest possible footprint
- ➖ Rust global-hook crates have weaker/patchier event-suppression support than SharpHook; the riskiest part gets riskier
- ➖ Slowest development for the team we actually have (Joel + agents iterate faster in C#/JS than Rust)

## Recommendation

**Option A.** Both halves matter — the engine's quality bars are unforgiving, and the UI is a real workbench — but only Electron treats them asymmetrically (first-class UI, engine exiled to a helper process). Avalonia serves both from one codebase: it is a full cross-platform desktop UI framework (WPF's spiritual successor, self-rendering via Skia, so the UI is *identical* on Windows and macOS — one implementation, no double-build). Tabs, sectioned lists, tree views, checkboxes/radios, data-bound list virtualization are all standard; gesture icons are a natural fit for its vector drawing (render each template's polyline + arrowhead straight into the list — no bitmap assets). Everything F7 requires is squarely inside what Avalonia does well; Electron's UI advantage only becomes decisive for web-canvas-heavy editors like Chimera, which this is not.

Note on familiarity: what actually transfers from Chimera is the *agent workflow* — CLAUDE.md conventions, docs taxonomy, read-first tables, screenshot-verification habits — and all of that is language-independent and already being replicated here.

## Consequences (if accepted)

- Repo stays a .NET solution; core engine and platform adapters get separate projects with interfaces at the boundaries (requirements N1/N3)
- The spike's SharpHook findings (SimpleGlobalHook-only suppression, `IsEventSimulated` for replay) become architecture constraints documented in reference docs
- macOS work later = implementing the platform-adapter interfaces + permissions onboarding + signing; no UI rewrite
