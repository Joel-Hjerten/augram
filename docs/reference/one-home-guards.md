# One-home guards

Plan 0007 folded about 80 jobs that were written several times into one home each. The architecture tests in `tests/Augram.App.Tests/Architecture/OneHome*Tests.cs` keep them folded: a second implementation of a listed job fails the normal test run, and the failure names the home to use. This page is the list. Before writing code in a folder, read that folder's README and use the type it names for the job; if the job is in the table below, the test will hold you to it.

## How the guards work

- **Reflection first** (exact): over the built assemblies, e.g. only three types derive from `Window`, `CommandItem` declares none of the header's fields, no Components record has a static `From`.
- **Project references** next: the csproj files (`TestSupport` references Core only; no `src/` project references test code) and the assemblies' references (the layering).
- **Source scans** last: `SourceScan` reads the `.cs` and `.axaml` files with comments removed (strings kept) and matches a pattern; a pattern may span lines. `SourceScan.AssertOnlyIn(pattern, files, useInstead, allowed…)` fails on a match outside the allowlist **and** on an allowlist entry that matches nothing any more, so the lists stay short and true.
- **Allowlists** are `Allowed(path, reason, onLine?)`: a file, or a folder ending in `/`, and optionally the text the line must contain. Every entry has a one-line reason. "Leftover, fold later" marks a known duplicate not yet folded (listed below).
- The guards run in well under a second (52 tests, about 1 s together including one headless pass over every dialog).

## The table

Paths are under `src/Augram.App/` unless they start with a project. Test classes are in `tests/Augram.App.Tests/Architecture/`.

| Job | One home | Banned elsewhere | Test |
|---|---|---|---|
| Reach the UI thread | `Hosting/UiThread` (`Post`, `RunOrPost`, `IsCurrent`, `Invoke`) | `Dispatcher.UIThread.Post/CheckAccess/Invoke…`; allowlisted: priority posts inside controls (render frames, `RowListHost`, `StepList.AfterMenu`, `CommandTree`'s rename, `ItemRow`, `SectionRow`), the window hand-over (`Send`), the gallery | `OneHomeHostingTests.OnlyUiThreadTouchesTheDispatcherDirectly` |
| Find the main window | `Dialogs/DialogHost.MainWindow` / `.Owner` | `….MainWindow` on the lifetime; allowlisted: `App.HandOver` | `OneHomeHostingTests.OnlyDialogHostLooksUpTheMainWindow` |
| Read a launch switch | `Hosting/Launch/` (`LaunchArguments`, `AppPaths`, `EngineKillSwitch`, `LaunchVisibility`) | `Environment.GetCommandLineArgs`, `"--no-engine"`, `"--hidden"`, `"--gallery"`, `"--config-folder"`, `"AUGRAM_NO_ENGINE"`, `desktop.Args` in `src/` | `OneHomeHostingTests.OnlyTheLaunchFolderReadsLaunchSwitches` |
| Give components their ports | `Hosting/Modules/FormServices.Publish` + `Components/ComponentPorts` | writing `Resources[…] =`, the port key, `TryFindResource("Augram.…")` | `OneHomeHostingTests.ComponentPortsArePublishedOnlyByFormServices` |
| Register a service | `CompositionRoot` + `Hosting/Modules/` | `Add…Singleton/Transient/Scoped` anywhere else | `OneHomeHostingTests.ServicesAreRegisteredOnlyInTheCompositionRootAndItsModules` |
| The shared dialogs; the config session and its stores | `DialogsModule`; `ConfigModule` | registering `IConfirmPresenter`/`IFormDialogPresenter`, `new ConfigSession`, registering the stores elsewhere; `TryAdd` in a module (allowlisted: `SyncModule`'s two presenters) | `OneHomeHostingTests.EachSharedServiceIsRegisteredByItsOwnModule` |
| Platform adapters | `Hosting/Modules/PlatformModule` | `Augram.Platform.Windows/MacOS` in the App; allowlisted: the macOS simulator wrappers, launch event, Dock and menu-bar item | `OneHomeHostingTests.OnlyThePlatformModuleBuildsPlatformAdapters` |
| Declare a tab | a module's `NavEntry` (`Hosting/Modules/`) | `new NavEntry(` (gallery excepted) | `OneHomeHostingTests.TabsAreDeclaredOnlyByTheirModules` |
| Build the main window | `Hosting/MainWindowFactory` | `AppearanceLink.Follow(`, resolving or `new`ing `MainWindow` | `OneHomeHostingTests.OnlyMainWindowFactoryBuildsTheMainWindow` |
| Show (activate) a window | `Hosting/MainWindowOpener.ShowNow`; `Dialogs/DialogHost` | `.Activate()` | `OneHomeHostingTests.WindowsAreActivatedOnlyByTheOpenerAndDialogHost` |
| One Augram at a time | `Hosting/Instance/` (`InstancePipe`) | named pipes, `NamedWaitHandleOptions`, `new Mutex` | `OneHomeHostingTests.OnlyTheInstanceFolderOpensPipesAndMutexes` |
| Start at login | `Hosting/Startup/StartAtLoginLink` | `IStartupRegistration` (PlatformModule registers it) | `OneHomeHostingTests.OnlyStartAtLoginLinkTalksToTheStartupRegistration` |
| The tray | `CompositionRoot` | `new AppTray(` | `OneHomeHostingTests.OnlyTheCompositionRootBuildsTheTray` |
| Walk the tabs (tools) | `Navigation/NavigationRegistry.LeafKeys` | `.SubEntries` in `tools/` | `OneHomeHostingTests.ToolsWalkTheNavigationThroughTheRegistry` |
| Which platform this is | `Augram.Core/Abstractions/RunningPlatform` (Engine: `EnginePorts.Platform`) | `OperatingSystem.IsWindows/IsMacOS`, `RuntimeInformation.IsOSPlatform`; allowlisted where the OS, a library or the toolkit needs the real answer (13 entries, each with its reason) | `OneHomePlatformTests.PlatformChecksGoThroughRunningPlatform` |
| A platform's name | `HostPlatformExtensions.DisplayName` | a `"Windows"` or `"macOS"` literal | `OneHomePlatformTests.OnlyHostPlatformExtensionsNamesAPlatform` |
| A log source | `Augram.Core/Diagnostics/LogSources` | a string literal as a log call's or `LogEvent`'s source | `OneHomePlatformTests.LogSourcesAreNeverLiterals` |
| Keys held around an injection | `Augram.Core/Abstractions/InputSimulation` | a loop that presses keys; leftover: `ScrollExecutor` | `OneHomePlatformTests.KeysAreHeldAroundAnInjectionOnlyThroughInputSimulation` |
| The layering (ADR-0002 §1) | Core references nothing; Engine, Platform, Sync.Git, Import only Core; Avalonia only in the App | an assembly reference against it (reflection) | `OneHomePlatformTests.ProjectsReferenceOnlyWhatTheLayeringAllows` |
| Names joined for display | `Augram.Core/Mapping/CommandNames.Label` / `.Parent` | `" › "` joins; allowlisted: a form's region path and a tab's path | `OneHomeFormsTests.NamesAreJoinedOnlyByCommandNames` |
| Retarget or unbind a gesture | `MappingStore.RetargetGesture` / `UnbindGesture` | `WithGestureReplaced(` outside `Core/Mapping/`, an `UpdateCommand` loop over `UsedBy(` | `OneHomeFormsTests.GesturesAreRetargetedOnlyByTheMappingStore` |
| A list's platform sides (original, own version, inferred; plan 0012) | `Augram.Core/Mapping/PlatformSides` (`PlatformList<T>`; `Command.IsInferredOn`, `EditableStepsFor`) | an origin taken apart and compared with a platform by hand (`Origin is { } origin && origin != platform`); allowlisted: `Command.Trigger.cs` (the trigger converts from the origin) | `OneHomeFormsTests.ThePlatformSideRulesLiveInPlatformSides` |
| What an app matcher matches | `AppMatcher.Summary` | `EffectiveProcessNames(`, `IsGuessedOn(`, `(guessed)`, "matches nothing yet", `"nothing on ` in the App | `OneHomeFormsTests.TheAppProjectsAMatcherOnlyThroughItsSummary` |
| The Global rename rule | `MappingRules.EnsureNotRenamingGlobal` | "cannot be renamed" in the App | `OneHomeFormsTests.TheGlobalRenameRuleIsTheMappingRules` |
| A side panel's form | `ViewModels/PanelEdit` (Commands: `SectionPanel`) | `bool _applying…`/`_syncing…`, `.PropertyChanged +=` in `CommandsViewModel*`, `IgnoredViewModel*`, `SectionPanel` | `OneHomeFormsTests.SidePanelFormsFollowThroughPanelEdit` |
| "Use on" | `ViewModels/UseOnEdit` | `PlatformSet.None.With(`, `"Use on"` | `OneHomeFormsTests.UseOnIsEditedOnlyByUseOnEdit` |
| A check list's ticks | `ViewModels/CheckedSet<TId>` | `HashSet<…> _checked`, `new CheckListItem(` in `ViewModels/` | `OneHomeFormsTests.CheckListsKeepTheirTicksInCheckedSet` |
| A check list's "unused" entries and their ✕ (the Not in and Also in dialogs) | `ViewModels/Commands/RemovableEntries` | `new CheckListRemove(`, `List<GroupId> _removed` in `ViewModels/` | `OneHomeFormsTests.RemovableEntriesOfACheckListLiveInRemovableEntries` |
| The commands naming an Exclusions entry (Per command through Not in, Global through Also in) | `Augram.Core/Mapping/MappingDocument.UsersOf` | `.NotIn.Contains(`, `.AlsoIn.Contains(` in the App | `OneHomeFormsTests.AnExclusionEntrysUsersAreLookedUpOnlyThroughUsersOf` |
| A form dialog | `Dialogs/FormDialogs` + `IDialogForm` | `new FormDialogRequest(` outside `Dialogs/` (allowlisted: `SyncJoinPresenter`) | `OneHomeFormsTests.FormDialogsAreRequestedThroughFormDialogs` |
| A screen's lines | `Declarations/ButtonsField`, `CommittedTextField`, a field and its renderer | `CustomField`, `new Button/TextBox/StackPanel/DockPanel/Grid/TextBlock` in `Screens/` | `OneHomeFormsTests.ScreensDeclareFieldsAndBuildNoControls` |
| Buttons on a form line | `Components/Fields/Buttons/` | the `field-line`/`field-buttons` classes; leftover: `RunStepForm`'s Browse… line | `OneHomeFormsTests.FieldLinesAreStyledOnlyByTheButtonsRenderer` |
| One nameless change from several sources | `Declarations/FormChanges` | a `PropertyChanged +=` that raises `string.Empty` | `OneHomeFormsTests.OnlyFormChangesRelaysChangesAsNameless` |
| Edit keys and their names | `Components/ListEditing/EditKeymap` | `"F2"`, `"Return"`, `"Enter"`, `"Ctrl+…"`, `"Cmd+…"`, `Key.F2` | `OneHomeComponentTests.EditKeysComeFromEditKeymap` |
| A list's menu and keys | `Components/ListEditing/ActionMenu<TAction>` | `new MenuItem`, `new KeyBinding` in `Components/` | `OneHomeComponentTests.MenusAndKeyBindingsAreBuiltByActionMenu` |
| Rename in place | `Components/ListEditing/ItemRow` | `PART_NameEditor` | `OneHomeComponentTests.OnlyItemRowFindsItsNameEditor` |
| A template's button | `Components/Parts/TemplateParts.OnClick` | `NameScope.Find<Button>(` | `OneHomeComponentTests.TemplateButtonsAreWiredByTemplateParts` |
| A toolbar button | `Components/Parts/ToolbarButton.Create` | `Classes.Add("toolbar")` | `OneHomeComponentTests.ToolbarButtonsAreMadeByToolbarButton` |
| A step form's state | `Components/Steps/StepFormState<TStep>` | a form's own `Emit`; leftovers: HDR, Window, Display mode, Open app | `OneHomeComponentTests.StepFormsKeepTheirStateInStepFormState` |
| Hotkey choices | `Components/Capture/HotkeyChoices` | `new ModifierToggle()`, `Enum.GetValues<KeyCode>()` | `OneHomeComponentTests.HotkeyChoicesAreBuiltByHotkeyChoices` |
| The header's own fields | `Components/CommandHeader/CommandHeaderItem` | `StepRefusals`, `ChangeRefusals`, `DraftNote`, `ConflictName`, `CanSwapTrigger`, `CanTakeTrigger`, `OptionsDragDistancePx`, `StrokeButton` on `CommandItem` (reflection) | `OneHomeComponentTests.HeaderOnlyFieldsLiveOnCommandHeaderItem` |
| Project an item for a component | `ViewModels/Commands/Projection/` | a static `From` on a Components record, `GestureTileItem` excepted (reflection) | `OneHomeComponentTests.ComponentItemsAreProjectedOutsideTheComponents` |
| A search's text comparison; a text box with a clear glyph | `Components/SearchBox/SearchQuery`; `Components/SearchBox/SearchBox` (plan 0009) | `Contains(…, OrdinalIgnoreCase)` / `StartsWith(…, OrdinalIgnoreCase)` under `Components/` and `ViewModels/` (allowlisted: `AppMatcherEditViewModel.Picking`'s process-name equality); `InnerRightContent` or a ✕ in `Components/` and the theme files outside the SearchBox template | `OneHomeComponentTests.SearchesCompareTextOnlyInSearchQuery`, `TheClearGlyphBelongsToTheSearchBox` |
| A list's selection, focus and clicks | `Components/ListEditing/RowListHost` | in the list components: `.SelectionChanged +=`, `.DoubleTapped +=`, `ContainerFromItem(…).Focus()`, `IsRightButtonPressed`, `bool _applying`, `ClickCount == 2`, a `ListBox` field, `.SelectedItem =`, `() => !IsEditing` | `OneHomeComponentTests.ListSelectionFocusAndClicksLiveInRowListHost` |
| The trigger draft, the panel, steps, lookups | `ViewModels/Commands/TriggerEditor`, `SectionPanel`, `StepEditor`, `CommandLookups` | those jobs' members declared on `CommandsViewModel` (reflection); `StepOffer.Check`, a `StepListAction` switch, "no longer exists" in `CommandsViewModel*.cs` (leftovers: a category's lookup, the hold remap's delete) | `OneHomeCommandsTests` (first two) |
| The collaborators' host | `ICommandsHost`, explicit only | an implicit implementation (the two the view binds anyway are listed) | `OneHomeCommandsTests.CommandsHostIsImplementedOnlyExplicitly` |
| The collaborators stay inside | internal, unreferenced from `Components/` and `Screens/` | a public collaborator, its name in those folders | `OneHomeCommandsTests.TheCollaboratorsStayInsideTheViewModels` |
| A window | `Views/MainWindow`, `Overlay/TrailOverlayWindow`, `Dialogs/DialogWindow` | any other type deriving from `Window` but not `DialogWindow` (reflection); `new Window`, a `<Window` XAML root | `OneHomeDialogTests.OnlyTheThreeBaseWindowsDeriveFromWindow`, `NoWindowIsBuiltOutsideTheBases` |
| Show and place a dialog | `Dialogs/DialogHost`, the `DialogWindow` constructor | `ShowDialog(` (allowlisted: `ConfirmPresenter`), `DialogSize.Apply`, `WindowStartupLocation` (allowlisted: the overlay) | `OneHomeDialogTests.DialogsAreShownAndPlacedOnlyByTheirBase` |
| A dialog's chrome | `Dialogs/DialogShell` (theme in `Themes/Default/Lists.axaml`) | `dialog-buttons`, `DockPanel.dialog`; every `DialogWindow` opened headless must show a `DialogShell`, and a new dialog must add its line to the test | `OneHomeDialogTests.DialogButtonsAreTheShells`, `EveryDialogIsOnTheShell` |
| The fakes of Core's ports | `tests/Augram.TestSupport` | a test type implementing a `Core.Abstractions` port, `IStepType` or `IStep`; a type named like a TestSupport type | `OneHomeTestCodeTests.OnlyTestSupportImplementsCorePorts`, `NoTestProjectRedeclaresATestSupportType` |
| Test code stays test code | TestSupport over Core only, no xunit | a `src/`/`tools/` project referencing TestSupport or a `*.Tests` project; TestSupport referencing more | `OneHomeTestCodeTests.TestCodeIsReferencedOnlyByTests` |

The older rules on the theme stay where they were: `ComponentColorTests` (no literal colour), `TextRoleTests` (text roles), `RowMetricsTests`, `HelpTextTests`, `ScrollBarLayoutTests`, `FieldKindCoverageTests`.

## Known leftovers (allowlisted, fold later)

- `Augram.Core/Steps/Scroll/ScrollExecutor`: its own bracket, because it stops at the first failed press and names the key (A19).
- `CommandsViewModel.Categories` and `.HoldRemaps`: "no longer exists" written by hand (no `RequireCategory` yet; the hold remap's delete asks before `Guard`).
- `Components/Steps/Run/RunStepForm`: the Browse… line is built by hand with `field-line`.
- The HDR, Window, Display mode and Open app step forms keep their own `Emit` (S14's watch list).

## Adding a guard when a new fold happens

1. Name the job and its one home in the folder's README ("which type for which job").
2. Add a test beside its area's class (`OneHome{Hosting,Platform,Forms,Component,Commands,Dialog,TestCode}Tests`): reflection if the rule is about types, a csproj check if it is about references, else a `SourceScan` pattern that matches what a second implementation would have to write. The message says what to use instead and cites the plan or ADR.
3. Allowlist only real exceptions, each with a reason; mark a duplicate you could not fold as "leftover, fold later" and list it above.
4. See it fail once: add a violating line (no rebuild is needed for a source scan: `dotnet test --no-build`), check the message, remove it.
5. Add its row here.
