# Augram.App

The Avalonia desktop app: tray, settings workbench, shared components, dev gallery. MVVM with `CommunityToolkit.Mvvm`; views are XAML plus a near-empty code-behind; view models are disposable projections over Core/Engine stores (ADR-0002 §5, §5a). Screens are *declared* as section/field trees and list specs, rendered by generic lookless components whose look comes from a theme (§5b, §5c).

**May reference:** `Augram.Core`, `Augram.Engine`, `Augram.Platform.Windows`, `Augram.Platform.MacOS`, and the Avalonia / CommunityToolkit.Mvvm / Microsoft.Extensions.DependencyInjection packages. `CompositionRoot.cs` is the one place services are registered; nothing else calls `new` on a service.

**Must never contain:** business rules, application state that outlives a window (the `AppState` flags are an M1 placeholder until the engine host and settings store own them), P/Invoke, or SharpHook.

## Folders

| Folder | Holds |
|---|---|
| `Declarations/` | The vocabulary screens are written in: `Field` kinds (`ToggleField`, `DropdownField<T>`, `TextField`, `NumberField`, `ButtonRadioField<T>`, `ColorField`, `NoteField`, `CustomField`), `Section`, the screen shapes (`FormScreen`, `ListScreen`, `TextScreen`), `ListSpec` and its parts, and the binding abstraction `IValueBinding<T>` / `DelegateBinding<T>`. Pure records; every node captures its source file and line for the inspector. |
| `Screens/` | One static class per screen, each a `Declare(...)` that returns a `ScreenDeclaration` bound to a view model. Rearranging a screen is editing this list. |
| `Navigation/` | `NavEntry`, `NavigationRegistry` (tabs with optional sub-tabs) and `AppNavigation`, the app's actual tab list. |
| `ViewModels/` | Projections over stores: `AppSettingsViewModel` (in-memory Core `Settings` until the store is wired), `HealthViewModel`, `LogViewModel`, `RecognitionViewModel`, `MainWindowViewModel`. |
| `Components/` | One folder per lookless component: `Shell`, `ScreenHost` (+ screen renderers), `SectionForm` (+ `SectionView`, `FieldRow`), `ItemList`, `TextPanel`, `Fields/<Kind>/` (one renderer per field kind, `Color/` also holds `ColorEditor`). No `.axaml` here carries a literal colour (architecture test). |
| `Themes/` | `Tokens.axaml` (semantic keys: `Brush.*`, `Color.*`, `Space.*`, `Pad.*`, `Font.*`, `Inspector.Depth*`), `Wireframe/Wireframe.axaml` (ControlThemes for every component + class styles for the standard editors), `Default/Default.axaml` (includes Wireframe until D2), `ThemeSelector` (the one line that picks the theme). |
| `Inspector/` | `Region` attached properties set by the renderers, `InspectorOverlay` (hold F1; Debug only), `InspectorReport` (the clipboard text). |
| `Hosting/` | Process-level services: `AppState`, `AppPaths`, `SingleInstanceGuard` (mutex + named pipe), `IClipboardText`/`AppClipboard`, `AppHealthContributor`. |
| `Tray/` | `AppTray` (Avalonia `TrayIcon` + menu), `ClickDiscriminator` (single vs double click), `TrayIconSet` (the two placeholder PNGs in `Assets/`). |
| `DevGallery/` | Debug-only: `GalleryNavigation` adds a Gallery tab with one sub-tab per component; `GalleryFakes` holds the fake data. `--gallery` opens it at start. |
| `Views/` | `MainWindow`: composes `Shell` and `InspectorOverlay`, owns no visuals; closing hides it. |

## Rules of the road

- **Theme rule.** A component never names a colour, font, margin or icon. It uses `{StaticResource Token}` inside its ControlTheme in `Themes/<Theme>/`, or a class (`field-editor`, `help`, `section-title`, `toolbar`, `cell`, ...) that the theme styles. `tests/Augram.App.Tests/Architecture/ComponentColorTests` fails the build on a literal colour under `Components/`.
- **Gallery rule.** A component is not done until `DevGallery/GalleryNavigation` shows it in its states with fake data. Same change, same review.
- **Declaration rule.** Screens never hand-lay XAML. If a screen needs something the vocabulary lacks, add a field kind or a `CustomField`, not a one-off view.
- **State rule.** View models forward intents to stores and services; they hold UI-only state (selected tab, filter choice). Deleting a view model loses nothing.

## How to add…

**A field kind.** (1) `Declarations/XField.cs`: a sealed record deriving from `Field` with `Kind => "X"`, a `IValueBinding<T>` and the `[CallerFilePath]`/`[CallerLineNumber]` parameters. (2) `Components/Fields/X/XFieldRenderer.cs`: `IFieldRenderer` with `Kind => "X"` that builds the editor and keeps it in step with the binding through `BindingObserver.Attach`. It is found by assembly scan; nothing else is edited. (3) If the editor is a new templated control, give it a ControlTheme in `Themes/Wireframe/Wireframe.axaml`. (4) Add it to the gallery's SectionForm page. `FieldKindCoverageTests` checks the `XField` → `"X"` convention.

**A component.** `Components/<Name>/<Name>.cs` deriving from `TemplatedControl` (properties in, events out, named `PART_*` parts), a `ControlTheme` keyed `{x:Type ns:Name}` in each theme file, a gallery page, and `Region.Mark` on anything the inspector should name.

**A screen.** `Screens/<Name>Screen.cs` with `Declare(viewModel)` returning a `FormScreen`, `ListScreen` or `TextScreen`. A new screen *shape* needs an `IScreenRenderer` under `Components/ScreenHost/` (also found by scan).

**A tab or sub-tab.** One `NavEntry` in `Navigation/AppNavigation.cs` pointing at the screen's `Declare`. Keys are unique across levels; `Shell.SelectedKey` can select any of them.

**A theme.** Copy `Themes/Wireframe/` to `Themes/<Name>/<Name>.axaml`, override tokens and ControlThemes, add the name to `ThemeSelector.Names`, and set `ThemeSelector.Active`.

## Tray and single instance (F7, A10)

Avalonia's `TrayIcon` raises only `Clicked`, so single versus double click is reconstructed by `ClickDiscriminator` with a 250 ms `DispatcherTimer`: a single click toggles `AppState.Enabled` 250 ms after the click; a second click inside the window opens the main window instead. The menu has Open · Enabled (checkable) · Start at login (checkable) · Quit. `SingleInstanceGuard` owns a named mutex and listens on a named pipe; a second launch signals it and exits. Diagnostics: `CompositionRoot` wires `ChannelEventLog` → `InMemorySink` (Diagnostics › Log) + `RollingFileSink` (`<config>/logs`), `HealthRegistry` (Diagnostics › Health, refreshed every second) and `RecognitionLog` (Diagnostics › Recognition); the engine host registers there in step 4b.
