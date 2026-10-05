# ADR-0002: Code and repo structure (modularity rules)

**Status: ACCEPTED** (Joel, 2026-10-05). Written at Joel's request ("sound and modular, no monolith classes or files"). Assumes ADR-0001 (.NET + SharpHook + Avalonia).

## Context

Both reference apps are cautionary tales. StrokesPlus (C++) is one 17,000-line file. GestureSign has a 684-line `ApplicationManager` and a 651-line `PointCapture` that mix hook handling, window lookup, timers, IPC and overlay control. Both work, both are hard to change safely, and both would be hostile to agents editing them in parallel. Augram's N3 requirement ("add an action type = one new folder, never scattered edits") and the plan to have agents do most of the coding make structure a first-class concern.

## Decision

### 1. Solution layout: five runtime projects, dependencies point inward

```
src/
  Augram.Core/                 pure .NET. No OS, UI, or hook library references. Ever.
  Augram.Engine/               wires Core to SharpHook: hook adapter, worker loop, executor, wheel trigger
  Augram.Platform.Windows/     window-under-point, process identity, window ops, native overlay fallback
  Augram.Platform.MacOS/       same contracts, implemented later (compiles as stubs until then)
  Augram.App/                  Avalonia: tray, settings workbench, learn dialog, overlay, components, dev gallery
  Augram.Import.StrokesPlus/   importer for StrokesPlus.net JSON; depends on Core only
tests/
  Augram.Core.Tests/           recognizer fixtures (Joel's 90 templates), state-machine scripts, config round-trips
  Augram.Engine.Tests/
  Augram.App.Tests/            Avalonia headless tests for components and view models
docs/                          as today (requirements, adr, reference, plans, learnings)
```

Allowed references: `App → Engine, Platform.*, Core` · `Engine → Core` · `Platform.* → Core` · `Import → Core` · `Core → nothing`. An architecture test in `Core.Tests` fails the build if `Augram.Core` ever references Avalonia, SharpHook, `System.Windows.*`, or `System.Drawing`.

### 2. Ports and adapters

Core owns the interfaces for everything that touches the outside world; the other projects implement them. Initial ports (all small, 1 to 5 members):

| Port (in Core) | Implemented by |
|---|---|
| `IInputSource` (button/move/wheel events in, suppress decision out) | Engine (SharpHook) |
| `IInputSimulator` (key/text/click/media key synthesis) | Engine (SharpHook), Platform for media keys if SharpHook lacks them |
| `IWindowSystem` (window under point, activate, identity: process name/path/title/class) | Platform.Windows / Platform.MacOS |
| `IWindowOperations` (close, minimize, maximize/restore, topmost, set bounds) | Platform.* |
| `IProcessLauncher` (run command line / program) | Engine or Platform |
| `IOverlay` (begin stroke, append points, end) | App (Avalonia window) with Platform fallback |
| `IClock`, `IConfigStore` | Core defaults, overridable in tests |

One composition root in `Augram.App` (`Program.cs` / `AppHost`) registers everything with `Microsoft.Extensions.DependencyInjection`. Nothing else calls `new` on a service.

### 3. Core is organised by subsystem, each a folder with a README

```
Augram.Core/
  Gestures/        Gesture, GestureSample, GestureLibrary (raw point lists, F2)
  Recognition/     Resampler, AngleSequence, GestureMatcher, MatchResult   (MIT port; NOTICE file beside it)
  Capture/         CaptureStateMachine, CaptureThresholds, CaptureEvent, CaptureOutcome
  Mapping/         AppGroup, Command, Trigger, AppMatcher, CommandResolver (global → app override → nothing)
  Steps/           IStep, IStepType, StepRegistry, StepExecutionContext
    Hotkey/        HotkeyStep (record) · HotkeyStepType (metadata + factory) · HotkeyExecutor
    TypeText/
    RunCommand/
    MediaKey/
    Delay/
  Config/          ConfigDocument (versioned), migrations, serialization
  Abstractions/    the ports in §2
```

`CaptureStateMachine` is a pure object: feed it `CaptureEvent`s and a clock, it returns `CaptureOutcome`s (suppress / replay click / stroke complete / wheel trigger / cancelled). It never touches a hook, a timer, or a window. That is what makes the hot path testable with scripted event sequences and keeps the hook handler at "append and return" (invariant 1).

### 4. Step types are vertical slices

Adding a step type means adding one folder under `Core/Steps/<Name>/` and one folder under `App/Components/Steps/<Name>/` (parameter form + view model), and registering the type in the `StepRegistry` from inside that folder (static registration list, or assembly scan; chosen in the plan). Nowhere in the codebase is there a `switch` over step kinds. Executors receive a `StepExecutionContext` (target window, gesture start point, services) and nothing else.

The same pattern applies to triggers (gesture, wheel up/down) and, later, to app matchers.

### 5. UI structure

- MVVM with `CommunityToolkit.Mvvm`. Views are XAML plus a near-empty code-behind. View models hold all logic and are tested headless.
- `App/Components/` holds every shared control, one folder per component: `GestureGlyph/`, `CommandTree/`, `StepList/`, `AppMatcherForm/`, `MasterDetail/`, `SectionToolbar/`, `ItemToolbar/`. Screens in `App/Views/` compose components and own no visuals of their own.
- `App/DevGallery/` is compiled only in Debug (`#if DEBUG` + a dev-mode flag) and shows every component in every state. A component is not done until it has a gallery entry.
- Theme tokens (colors, spacing, type) live in one resource dictionary; components reference tokens, never literals.

### 5a. State and logic live below the UI (Joel, 2026-10-05)

The UI will be rearranged repeatedly. Nothing of value may be lost when a screen is deleted or a component moves.

- **Application state lives in stores, not in view models or controls.** `GestureLibrary`, `MappingStore`, `Settings`, `EngineStatus` and the training session are owned by Core/Engine services, created once in the composition root, and outlive every window. Closing the settings window and reopening it reconstructs view models from the stores; it never reconstructs state.
- **View models are disposable projections.** They subscribe to a store, expose what one screen needs, and forward intents (`AddStep`, `RenameGesture`, `StartTraining`) as calls on a store or service. They hold no business rules and no derived data that a store could compute. Deleting a view model loses nothing.
- **Business rules have exactly one home, in Core.** "A command needs a gesture or a trigger", "a gesture name must be unique", "an app override shadows the global command", "this stroke matches gesture X at 84%" are Core functions, called by view models and by the engine alike. The UI only displays the result; it never re-implements a rule for validation.
- **UI-only state is allowed in view models**: selected tab, expanded groups, scroll position, unsaved form text. The test: if the state would be meaningless without this screen, it belongs to the screen; otherwise it belongs to a store.
- **Training is a Core/Engine concern.** A `TrainingSession` service owns the mode switch, the captured strokes, the best-match computation, and save-as-new vs add-sample. The learn dialog is a thin view over it, so the same session could later be driven from a different screen, a hotkey, or a test.
- **No service reaches into the UI.** Core and Engine never reference a view, a dispatcher, or Avalonia types. Marshalling to the UI thread is the view model's job, done in one base class.
- **Components are presentational.** A shared component takes data in and raises events out. It never calls a store or service itself; its host view model does. This is what lets a component appear in the dev gallery with fake data.

### 5b. Wireframe first, skin later (Joel, 2026-10-05)

The app structure (sections, navigation, which list sits where, what the step panel shows) must be testable and decidable before any visual design exists, and the visual design must be replaceable without touching structure or logic.

- **Every shared component is a lookless control with a swappable template.** The component defines its API (properties in, events out) and its named parts; its appearance comes from a theme resource dictionary. Avalonia's templated controls and `ControlTheme` are built for exactly this.
- **Two themes from day one:** `Themes/Wireframe/` (grey boxes, 1 px borders, system font, no icons beyond text labels, placeholder glyphs) and later `Themes/Default/` (the real design). The app runs on Wireframe until D2 is decided. Switching is one line in the composition root; the dev gallery can show both side by side.
- **Layout belongs to the component's template, not to the screen.** Screens only say "CommandTree here, StepPanel there" using `MasterDetail`. Moving the step panel from below to beside the command list is a template change, not a view-model or view change.
- **The wireframe is real code, not a mock.** It binds to real view models and stores, so the structure is tested with real data (Joel's imported config) from the first runnable build. The only thing thrown away later is the Wireframe theme's resource dictionary, and even that stays as a gallery option.
- **Rule for agents:** a component may not be written with inline colors, fonts, margins or icons; those come from theme tokens, or the component cannot be re-skinned. The architecture test for App checks that no `.axaml` under `Components/` contains a literal color.

### 5c. Screens are declared, not hand-laid (Joel, 2026-10-05)

Rearranging a screen (adding a subsection, moving a checkbox, turning a text field into a dropdown) must be a one-place edit, not XAML surgery.

- **A screen is a declaration.** Each screen's content is described in a view model as a tree of `Section` → `Field` nodes, where a field has a kind (`Toggle`, `Dropdown`, `Text`, `Number`, `Hotkey`, `Color`, `ButtonRadio`, `Note`, `Custom(component)`), a label, a help line, and a binding to a store value. A generic `SectionForm` component renders any such tree with the current theme. Options, App Definition, the step parameter forms and the training panel are all `SectionForm` trees. Reordering or nesting is editing that list.
- **Lists and trees are declared the same way:** `ListSpec` (columns, row template = a component, toolbar actions) rendered by one `ItemList` component. The Gestures grid, the command tree, the ignored-apps list and the step list are four specs, one renderer.
- **Navigation is data too:** the section list (Gestures, Commands, Ignored, Options) is a registry; adding a section is adding an entry and a screen declaration.
- Custom components (GestureGlyph, GestureDrawArea) plug into a tree as a `Custom` field, so even special screens stay declarative around their special part.
- Field kinds live one per folder under `App/Components/Fields/` and self-register, like step types. Adding a field kind never touches `SectionForm`.

### 5d. Layout inspector: hold F1 (Joel, 2026-10-05; good-to-have, dev/debug builds)

While F1 is held, an adorner layer draws the outline and name of every region on screen: sections, fields, components, lists, and the named parts inside components. Names come from an attached `Region.Name` property that `SectionForm`, `ItemList` and every shared component set automatically from their declaration, so the overlay needs no per-screen work. Nested regions use nested colors from the theme tokens. Purpose: Joel and agents can look at a running screen and see exactly which declaration node produced which pixels, which makes "move this under that" requests unambiguous. Avalonia's built-in DevTools (F12) stays available for the raw visual tree; F1 shows the *declared* structure instead.

**F1 + left-click on a region copies its identity to the clipboard** (Joel, 2026-10-05): the declaration path (screen › section › field), the component type and instance name, the source file and line of the declaration node, the view-model property it binds to, and the current theme name. Pasting that into a chat with an agent is enough to locate the exact code. The copied text is plain, one line per item.

### 6. Size and shape rules (enforced in review and by CLAUDE.md)

- A file holds one public type. Target under 200 lines; a file over 300 lines is a smell that needs a reason in a comment or gets split.
- No `*Manager`, `*Helper`, `*Utils` classes. Name by role: `GestureMatcher`, `CommandResolver`, `HotkeyExecutor`.
- Data is immutable `record` types. State changes go through one store per aggregate (`GestureLibrary`, `MappingStore`, `Settings`) with explicit save; the UI never mutates model objects in place.
- Threads are named and documented in `docs/reference/threading.md` when Engine lands: hook thread → bounded channel → engine worker → UI dispatcher. No other thread hops.
- Every subsystem folder has a `README.md` (purpose, invariants, what may reference it). CLAUDE.md's read-first table points at those READMEs rather than growing itself.

### 7. Tooling baked in from the first commit

- `Directory.Build.props`: `net10.0`, `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors`, analyzers on (`EnableNETAnalyzers`, `AnalysisLevel` latest), `InvariantGlobalization`.
- `Directory.Packages.props` for central package versions.
- `.editorconfig` with naming and style rules so agents produce uniform code.
- `dotnet format --verify-no-changes` and the test suite run in CI (GitHub Actions, Windows runner; macOS runner added when the Mac adapter exists).

## Consequences

- Core can be developed and tested with no hook, no window, no Avalonia, on any OS, including by agents in worktrees working on different subsystems without conflicts.
- The macOS port is `Augram.Platform.MacOS` plus overlay tuning. Nothing above the ports changes.
- The spike's code is retired, not promoted; its findings are already captured in CLAUDE.md invariants and the reference docs.
- The cost is ceremony: more projects, interfaces for things that have one implementation today. Accepted deliberately; the alternative is the 17,000-line file.
