# Augram.App

The Avalonia desktop app: tray, settings workbench, shared components, dev gallery. MVVM with `CommunityToolkit.Mvvm`; views are XAML plus a near-empty code-behind; view models are disposable projections over Core/Engine stores (ADR-0002 §5, §5a). Screens are *declared* as section/field trees and list specs, rendered by generic lookless components whose look comes from a theme (§5b, §5c).

**May reference:** `Augram.Core`, `Augram.Engine`, `Augram.Platform.Windows`, `Augram.Platform.MacOS`, and the Avalonia / CommunityToolkit.Mvvm / Microsoft.Extensions.DependencyInjection packages. `CompositionRoot.cs` is the one place services are registered; nothing else calls `new` on a service.

**Must never contain:** business rules, application state that outlives a window (stores and the engine own it; `AppState` only projects two settings for the tray), P/Invoke, or SharpHook.

## Folders

| Folder | Holds |
|---|---|
| `Declarations/` | The vocabulary screens are written in: `Field` kinds (`ToggleField`, `DropdownField<T>`, `TextField`, `NumberField`, `ButtonRadioField<T>`, `ColorField`, `NoteField`, `CustomField`), `Section`, the screen shapes (`FormScreen`, `ListScreen`, `TextScreen`), `ListSpec` and its parts, and the binding abstraction `IValueBinding<T>` / `DelegateBinding<T>`. Pure records; every node captures its source file and line for the inspector. |
| `Screens/` | One static class per screen, each a `Declare(...)` that returns a `ScreenDeclaration` bound to a view model. Rearranging a screen is editing this list. |
| `Navigation/` | `NavEntry`, `NavigationRegistry` (tabs with optional sub-tabs) and `AppNavigation`, the app's actual tab list. |
| `ViewModels/` | Projections over stores: `AppSettingsViewModel` (over `SettingsStore`, see "How settings flow"), `GesturesViewModel` (over `GestureLibrary`, see "Gestures tab"), `TrainingViewModel` (over `TrainingSession`), `ImportViewModel`, `HealthViewModel`, `LogViewModel`, `RecognitionViewModel`, `MainWindowViewModel`. |
| `Components/` | One folder per lookless component: `Shell`, `ScreenHost` (+ screen renderers), `SectionForm` (+ `SectionView`, `FieldRow`), `ItemList`, `TextPanel`, `GestureGlyph` (+ `GlyphGeometry`, the F4 recipe), `GestureGrid` (+ `GestureTile`, `GestureTileItem`, `GestureGridAction`, `GestureGridKeymap`), `GestureDrawArea` (+ `ScreenArea`), `Fields/<Kind>/` (one renderer per field kind, `Color/` also holds `ColorEditor`). No `.axaml` here carries a literal colour (architecture test). |
| `Training/` | The F3 training service and popup: `TrainingSession` (state: request, stroke, best match, Accept/Cancel; implements `ITrainingSession` for the engine), `TrainingRequest`, `TrainingOutcome`, `TrainingRouting` (the `EngineEvent` extension), `ITrainingPresenter`/`TrainingPresenter`, `TrainingWindow` + `TrainingView`. |
| `Import/` | The F8 StrokesPlus.net import flow: `IImportPresenter`/`ImportPresenter` (file picker + dialog), `ImportDialog` + `ImportView`, `ImportConflictItem`. The reading and merging live in `Augram.Import.StrokesPlus`. |
| `Themes/` | `Tokens.axaml` (semantic keys: `Brush.*`, `Color.*`, `Space.*`, `Pad.*`, `Font.*`, `Inspector.Depth*`), `Wireframe/Wireframe.axaml` (ControlThemes for every component + class styles for the standard editors), `Default/Default.axaml` (includes Wireframe until D2), `ThemeSelector` (the one line that picks the theme). |
| `Inspector/` | `Region` attached properties set by the renderers, `InspectorOverlay` (hold F1; Debug only), `InspectorReport` (the clipboard text). |
| `Hosting/` | Process-level services: `EngineModule` (the engine slice of the composition root, see below), `EngineModuleOptions`, `EngineSettingsLink`, `StrokeButtonDetection`, `TimerSaveScheduler`, `AppState` (tray flags projected over the settings store), `AppPaths` (config folder, `--config-folder`), `SingleInstanceGuard` (mutex + named pipe), `IClipboardText`/`AppClipboard`, `AppHealthContributor`. |
| `Overlay/` | The trail overlay: `TrailOverlayWindow` (the `IStrokeTrail`), `TrailBuffer` (worker-to-UI hand-off), `TrailCanvas`, `TrailFrame`. A window, not a component: see below. |
| `Tray/` | `AppTray` (Avalonia `TrayIcon` + menu), `ClickDiscriminator` (single vs double click), `TrayIconSet` (the two placeholder PNGs in `Assets/`). |
| `DevGallery/` | Debug-only: `GalleryNavigation` adds a Gallery tab with one sub-tab per component; `GalleryFakes` holds the fake data, `GestureGalleryPages` the Gestures-tab pages (glyph, grid, draw area, training content, import content). `--gallery` opens it at start. |
| `Views/` | `MainWindow`: composes `Shell` and `InspectorOverlay`, owns no visuals; closing hides it. |

## Rules of the road

- **Theme rule.** A component never names a colour, font, margin or icon. It uses `{StaticResource Token}` inside its ControlTheme in `Themes/<Theme>/`, or a class (`field-editor`, `help`, `section-title`, `toolbar`, `cell`, ...) that the theme styles. `tests/Augram.App.Tests/Architecture/ComponentColorTests` fails the build on a literal colour under `Components/`.
- **Gallery rule.** A component is not done until `DevGallery/GalleryNavigation` shows it in its states with fake data. Same change, same review.
- **Declaration rule.** Screens never hand-lay XAML. If a screen needs something the vocabulary lacks, add a field kind or a `CustomField`, not a one-off view.
- **State rule.** View models forward intents to stores and services; they hold UI-only state (selected tab, filter choice). Deleting a view model loses nothing.

## How to add…

**A field kind.** (1) `Declarations/XField.cs`: a sealed record deriving from `Field` with `Kind => "X"`, a `IValueBinding<T>` and the `[CallerFilePath]`/`[CallerLineNumber]` parameters. (2) `Components/Fields/X/XFieldRenderer.cs`: `IFieldRenderer` with `Kind => "X"` that builds the editor and keeps it in step with the binding through `BindingObserver.Attach`. It is found by assembly scan; nothing else is edited. (3) If the editor is a new templated control, give it a ControlTheme in `Themes/Wireframe/Wireframe.axaml`. (4) Add it to the gallery's SectionForm page. `FieldKindCoverageTests` checks the `XField` → `"X"` convention.

**A component.** `Components/<Name>/<Name>.cs` deriving from `TemplatedControl` (properties in, events out, named `PART_*` parts), a `ControlTheme` keyed `{x:Type ns:Name}` in each theme file, a gallery page, and `Region.Mark` on anything the inspector should name.

**A screen.** `Screens/<Name>Screen.cs` with `Declare(viewModel)` returning a `FormScreen`, `ListScreen`, `TextScreen` or `ComponentScreen` (one shared component filling the tab, bound to the view model in `Declare`; the Gestures grid). A new screen *shape* needs an `IScreenRenderer` under `Components/ScreenHost/` (also found by scan).

**A tab or sub-tab.** One `NavEntry` in `Navigation/AppNavigation.cs` pointing at the screen's `Declare`. Keys are unique across levels; `Shell.SelectedKey` can select any of them.

**A theme.** Copy `Themes/Wireframe/` to `Themes/<Name>/<Name>.axaml`, override tokens and ControlThemes, add the name to `ThemeSelector.Names`, and set `ThemeSelector.Active`.

## Tray and single instance (F7, A10)

Avalonia's `TrayIcon` raises only `Clicked`, so single versus double click is reconstructed by `ClickDiscriminator` with a 250 ms `DispatcherTimer`: a single click toggles `AppState.Enabled` 250 ms after the click; a second click inside the window opens the main window instead. The menu has Open · Enabled (checkable) · Start at login (checkable) · Quit. `SingleInstanceGuard` owns a named mutex and listens on a named pipe; a second launch signals it and exits. Diagnostics: `CompositionRoot` wires `ChannelEventLog` → `InMemorySink` (Diagnostics › Log) + `RollingFileSink` (`<config>/logs`), `HealthRegistry` (Diagnostics › Health, refreshed every second) and `RecognitionLog` (Diagnostics › Recognition); the engine host registers there in step 4b.

## EngineModule (the engine slice of the composition root)

`CompositionRoot.Build` calls `EngineModule.Register(services, …)` after the diagnostics services; `App.StartDesktop` calls `EngineModule.Start(services)` once the main window exists. `Register` adds, in order: `IClock`; `ConfigSession` over a `FileConfigStore` at `AppPaths.ConfigFolder` (load notices go to the log as source `config`; a first run writes the defaults and starter gestures at once; saves are debounced by `TimerSaveScheduler`, a `System.Threading.Timer` whose callback is marshalled to the UI thread so the session stays single-writer); `SettingsStore` and `GestureLibrary` from the session; the input source (`SharpHookInputSource`, or the factory in `EngineModuleOptions.InputSource`, which tests set to a fake so no hook is installed) and `SharpHookInputSimulator`; the Platform adapters (`Win32CursorProbe`, `Win32SystemEvents`, `OverlayWindowStyle`, `RunKeyStartupRegistration`; `PlatformAdapters = false` swaps in the null objects); `TrailOverlayWindow` as `IStrokeTrail`; `EngineHost` with gestures = `GestureLibrary.All`, recognition options = `Settings.Current.Recognition`, and the initial stroke button, thresholds, ignore key and enabled flag read from the store; `EngineSettingsLink`; `StrokeButtonDetection`. `Start` shows the overlay, resolves the host (which constructs the overlay on the UI thread), subscribes the link, syncs start-at-login with the OS, and starts the hook. Disposal runs in reverse through the service provider: the host stops the hook, the session flushes a pending save, the log drains last.

## How settings flow

`SettingsStore` is the only mutable owner of `Settings` (ADR-0002 §5a). Writers: `AppSettingsViewModel` setters (one undo step each), `AppState.Enabled` / `StartAtLogin` (tray and Options), `StrokeButtonDetection` (F1 detect-to-assign: `EngineHost.CaptureNextButtonPress`, 5 s timeout), and the Undo / Redo buttons on the Options page. Readers: `EngineSettingsLink` pushes the stroke button, ignore key, thresholds and enabled flag into `EngineHost` on every `Changed` (undo included); the recognizer reads `Recognition` through a delegate per stroke; the overlay reads `Trail` at every `Begin`; `AppState` applies `StartAtLogin` to `IStartupRegistration` and raises `PropertyChanged` for the tray. The view model raises `PropertyChanged` with an empty name on every store change, so every `DelegateBinding` on the page refreshes at once. The config folder is read-only on the page; `--config-folder <path>` (or `=path`) changes it (A17 minimal).

## Why the overlay is a Window, not a component

`Overlay/TrailOverlayWindow` is the one piece of UI with no declaration, theme or gallery entry. It is a top-level: it must cover every monitor, sit above every app, never take focus and never take a click, which are window properties (`Topmost`, `ShowActivated = false`, `IsHitTestVisible = false`, the Win32 extended styles re-applied through `IOverlayWindowStyle` after `Show()`, learnings 0001 B2). Its colour, width and opacity are user settings (F6), not theme tokens, so the theme rule does not apply. It is created and shown empty at startup and never hidden (the shown-and-empty variant measured 0.3–6 ms to first segment; a re-show drops the native styles). Trail calls arrive on the engine worker and go through `TrailBuffer`: append under a lock, post at most one `Dispatcher.UIThread.Post` until the UI thread has taken the frame. Pen width = `Trail.WidthPx` × the scaling of the screen under the stroke start (÷ the window's own scaling, since the window spans monitors). The first rendered segment is timed from `Begin` via `RequestAnimationFrame`, logged as `overlay` / `Trail first frame` and contributed to health as `OverlayFirstFrameMs`.

## Gestures tab (F3, F4, F5a, A7; plan 0001 M1 step 7)

`GesturesModule.Register(services)` is the tab's slice of the composition root (training session, presenters, `GesturesViewModel`); `GesturesModule.NavEntry(services)` is its tab entry. It expects `GestureLibrary`, `SettingsStore` and `IEventLog` to be registered and falls back to the starter set and default recognition options when they are not (tests, gallery).

- **GestureGlyph** draws a gesture's first sample: `GlyphGeometry.Normalise` fits the bounding box into the tile on the longer axis (aspect ratio kept, centred inside `Padding`), the polyline gets round caps and an arrowhead on the last segment; `:inactive` greys it through the theme. The geometry is rebuilt only when the points or the size change.
- **GestureGrid** is presentational: `Tiles` (sorted by the view model), `CanUndo`/`CanRedo`, `Message`, `Diagnostic` in; one `ActionRequested` event out carrying a `GestureGridAction` (New, AddSample, Rename, ToggleActive, Delete, Import, Undo, Redo), the selected tile and, for Rename, the new name. Toolbar buttons, the right-click menu, the keymap (`GestureGridKeymap`: F2 on Windows / Return on macOS to rename, Ctrl/Cmd+N, Delete, Ctrl/Cmd+Z, Ctrl+Y / Cmd+Shift+Z) and double-click (Add sample) all end in that event. Rename edits in place inside `GestureTile`; the list's key bindings stand down while an editor has focus. Delete asks nothing; Undo brings it back (A9).
- **GesturesViewModel** turns actions into `GestureLibrary` calls and shows the rule message (`GestureValidationException`) when one is rejected. The A7 line under the grid comes from `Core.Recognition.ConfusionCheck` (pairs of active gestures whose first samples score each other at or above the cut-off halfway between the threshold and 100), recomputed 200 ms after the last library change.

## Training (F3, A6)

`TrainingSession` owns the state: `Begin(TrainingRequest)` (new gesture, prefilled "New gesture N", or add a sample to an existing one), one stroke that each `ReplaceStroke` replaces (no averaging), the live best match from `GestureMatcher.Rank` against the active library, `Accept()` (adds or appends through the library's rules; the exception message is shown inline) and `Cancel()`. `TrainingPresenter` shows it as `TrainingWindow` (owned by the main window, centred, Cancel and Accept; Escape cancels, closing the window cancels). `GestureDrawArea` accepts left-button strokes and publishes its `ScreenArea` (physical screen pixels plus render scaling) whenever it moves; `TrainingSession.PublishCanvas` keeps the latest.

Stroke-button routing: the hook suppresses the stroke button, so the window never sees it. The engine wiring calls `ITrainingSession.TryConsume(points, startX, startY)` (or the `TrainingRouting.TryConsume(EngineEvent)` extension) for every completed stroke; when a session is open and the start point is inside the canvas, the points are converted to canvas coordinates, the stroke replaces the current one on the UI thread, and the call returns true. Safe from the engine worker thread. In M1 the engine still ranks and logs the stroke first; that is accepted.

## Import (F8, M1 step 10)

`ImportPresenter` opens a file picker (starting at `%APPDATA%\StrokesPlus.net\` with `StrokesPlus.net.json` suggested when it exists), then `ImportDialog` over `ImportViewModel`: the stats line ("Found N gestures, M samples; K actions and A apps will import in a later version"), the importer's warnings, and the merge plan with a Keep mine / Take theirs / Keep both choice per conflict (default Keep mine) plus "apply to all". Apply runs `GestureMerge.Apply` and `GestureLibrary.ReplaceAll` (one undo step) and logs an Info to source `import` with the counts.
