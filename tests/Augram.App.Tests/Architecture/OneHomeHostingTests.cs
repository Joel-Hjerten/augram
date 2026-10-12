using System.Text.RegularExpressions;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// One home per process-level job (plan 0007 S11, S12; docs/reference/one-home-guards.md): the UI thread, the main window,
/// launch switches, component ports, service registration, windows activated, the single-instance pipe, start at login, the tray.
/// Each test fails on a second implementation and names the home to use.
/// </summary>
public sealed partial class OneHomeHostingTests
{
    private const string App = "src/Augram.App/";

    private static IEnumerable<string> AppCode => Code("src/Augram.App", "tools");

    [Fact]
    public void OnlyUiThreadTouchesTheDispatcherDirectly()
        => AssertOnlyIn(
            DispatcherCall(),
            AppCode,
            "Use Hosting/UiThread (Post, RunOrPost, IsCurrent, Invoke) instead (plan 0007 S11). A post with an explicit DispatcherPriority is UI-only timing inside a control, kept only where allowlisted.",
            new Allowed(App + "Hosting/UiThread.cs", "the home"),
            new Allowed(App + "App.Launch.cs", "hands the window to the lifetime as the main loop's first job (Send priority)", "DispatcherPriority.Send"),
            new Allowed(App + "Overlay/TrailOverlayWindow.cs", "a render frame (Render priority)", "DispatcherPriority.Render"),
            new Allowed(App + "Overlay/NativeTrailOverlay.cs", "a render frame (Render priority)", "DispatcherPriority.Render"),
            new Allowed(App + "Components/ListEditing/RowListHost.cs", "focus after layout, rename after the click (S14b)"),
            new Allowed(App + "Components/ListEditing/ItemRow.cs", "commit the rename after the editor's own key handling"),
            new Allowed(App + "Components/StepList/StepList.cs", "open the menu after the click settles (StepList.AfterMenu, S14b)", "DispatcherPriority.Background"),
            new Allowed(App + "Components/CommandTree/CommandTree.cs", "rename after the double click (RenameByDoubleClick, S14b)"),
            new Allowed(App + "Components/CommandTree/SectionRow.cs", "raise the toggle after the expander's own click"),
            new Allowed(App + "DevGallery/CaptureGalleryPage.cs", "the gallery starts a capture once the page is in the tree"));

    [Fact]
    public void OnlyDialogHostLooksUpTheMainWindow()
        => AssertOnlyIn(
            MainWindowLookup(),
            AppCode,
            "Use Dialogs/DialogHost.MainWindow (or .Owner) instead (plan 0007 S12/S19).",
            new Allowed(App + "Dialogs/DialogHost.cs", "the home"),
            new Allowed(App + "App.Launch.cs", "App hands the window to the lifetime (HandOver)", "desktop.MainWindow = window"));

    [Fact]
    public void OnlyTheLaunchFolderReadsLaunchSwitches()
        => AssertOnlyIn(
            LaunchSwitch(),
            Code("src"),
            "Use Hosting/Launch/LaunchArguments (LaunchArguments.Current, AppPaths.ConfigFolder, EngineKillSwitch.IsSet, LaunchVisibility) instead (plan 0007 S12).",
            new Allowed(App + "Hosting/Launch/", "the home: the switches and their parse"));

    [Fact]
    public void ComponentPortsArePublishedOnlyByFormServices()
        => AssertOnlyIn(
            PortResource(),
            AppCode,
            "Publish ports with Hosting/Modules/FormServices.Publish and read them with Components/ComponentPorts.Near(control) / OfApplication() (plan 0007 S12).",
            new Allowed(App + "Components/ComponentPorts.cs", "the home: the key and the readers"),
            new Allowed(App + "Hosting/Modules/FormServices.cs", "the home: the one writer"),
            new Allowed(App + "Views/MainWindow.axaml.cs", "the title bar's padding, a window resource, not a port", "TitleBar.Padding"));

    [Fact]
    public void ServicesAreRegisteredOnlyInTheCompositionRootAndItsModules()
        => AssertOnlyIn(
            Registration(),
            AppCode,
            "Register services in CompositionRoot.cs or the slice's module in Hosting/Modules/ (plan 0007 S12; ADR-0002 §2).",
            new Allowed(App + "CompositionRoot.cs", "the home"),
            new Allowed(App + "Hosting/Modules/", "the home: one module per slice"));

    [Fact]
    public void EachSharedServiceIsRegisteredByItsOwnModule()
    {
        AssertOnlyIn(
            PresenterRegistration(),
            AppCode,
            "The confirmation and the form dialog are registered once, by Hosting/Modules/DialogsModule; other slices resolve them (plan 0007 S12).",
            new Allowed(App + "Hosting/Modules/DialogsModule.cs", "the home"));
        AssertOnlyIn(
            StoreRegistration(),
            AppCode,
            "The config session and its stores (SettingsStore, GestureLibrary, MappingStore) are created and registered once, by Hosting/Modules/ConfigModule; other slices resolve them (plan 0007 S12).",
            new Allowed(App + "Hosting/Modules/ConfigModule.cs", "the home"),
            new Allowed(App + "DevGallery/TransferGalleryPages.cs", "the gallery's page runs over a fake config store", "new ConfigSession("));
        AssertOnlyIn(
            TryAdd(),
            Code(App + "Hosting/Modules"),
            "A module registers what it owns with Add…; it never TryAdds a fallback for another slice's service (plan 0007 S12).",
            new Allowed(App + "Hosting/Modules/SyncModule.cs", "sync's two presenters, which its tests replace before registering the module", "Presenter"));
    }

    [Fact]
    public void OnlyThePlatformModuleBuildsPlatformAdapters()
        => AssertOnlyIn(
            PlatformNamespace(),
            AppCode,
            "Resolve the Core port (IWindowSystem, IClipboard, …) that Hosting/Modules/PlatformModule registers instead of using an Augram.Platform type (plan 0007 S12).",
            new Allowed(App + "Hosting/Modules/PlatformModule.cs", "the home"),
            new Allowed(App + "Hosting/Modules/EngineModule.cs", "the macOS simulator wrappers around SharpHook's simulator"),
            new Allowed(App + "App.Launch.cs", "the macOS launch event, observed before the main loop"),
            new Allowed(App + "Hosting/MacDockPresence.cs", "the macOS Dock icon follows the main window"),
            new Allowed(App + "Tray/MacTrayHost.cs", "the macOS menu-bar item is native"),
            new Allowed(App + "Tray/TrayMenuEntries.cs", "the macOS menu-bar item's menu"),
            new Allowed(App + "Themes/WindowBackdrop.cs", "the macOS blur view's material and unfocused state"));

    [Fact]
    public void TabsAreDeclaredOnlyByTheirModules()
        => AssertOnlyIn(
            NewNavEntry(),
            AppCode,
            "A tab is a module's NavEntry in Hosting/Modules/, listed in Navigation/AppNavigation.Build (plan 0007 S12).",
            new Allowed(App + "Hosting/Modules/", "the home"),
            new Allowed(App + "DevGallery/", "the gallery's own fake tabs"));

    [Fact]
    public void OnlyMainWindowFactoryBuildsTheMainWindow()
        => AssertOnlyIn(
            MainWindowBuild(),
            AppCode,
            "Use Hosting/MainWindowFactory.Create (the window with its look) instead (plan 0007 S12).",
            new Allowed(App + "Hosting/MainWindowFactory.cs", "the home"));

    [Fact]
    public void WindowsAreActivatedOnlyByTheOpenerAndDialogHost()
        => AssertOnlyIn(
            Activate(),
            AppCode,
            "Show the main window with Hosting/MainWindowOpener.ShowNow (or Open from another thread); show a dialog with Dialogs/DialogHost (plan 0007 S12).",
            new Allowed(App + "Hosting/MainWindowOpener.cs", "the home: the main window"),
            new Allowed(App + "Dialogs/DialogHost.cs", "the home: dialogs"));

    [Fact]
    public void OnlyTheInstanceFolderOpensPipesAndMutexes()
        => AssertOnlyIn(
            PipeOrMutex(),
            AppCode,
            "One Augram at a time lives in Hosting/Instance/ (InstancePipe's names and options, InstanceListener, InstanceClient) (plan 0007 S12).",
            new Allowed(App + "Hosting/Instance/", "the home"));

    [Fact]
    public void OnlyStartAtLoginLinkTalksToTheStartupRegistration()
        => AssertOnlyIn(
            StartupRegistration(),
            AppCode,
            "Use Hosting/Startup/StartAtLoginLink (and AppState over it) instead of IStartupRegistration (plan 0007 S12).",
            new Allowed(App + "Hosting/Startup/StartAtLoginLink.cs", "the home"),
            new Allowed(App + "Hosting/Modules/PlatformModule.cs", "registers the adapter"));

    [Fact]
    public void OnlyTheCompositionRootBuildsTheTray()
        => AssertOnlyIn(
            NewAppTray(),
            AppCode,
            "The tray is built once, in CompositionRoot (plan 0007 S12).",
            new Allowed(App + "CompositionRoot.cs", "the home"));

    [Fact]
    public void ToolsWalkTheNavigationThroughTheRegistry()
        => AssertOnlyIn(
            SubEntries(),
            Code("tools"),
            "Use Navigation/NavigationRegistry.LeafKeys instead of walking the entries by hand (plan 0007 S12).");

    [GeneratedRegex(@"\bDispatcher\.UIThread\.(?:Post|CheckAccess|VerifyAccess|Invoke|InvokeAsync)\b")]
    private static partial Regex DispatcherCall();

    // The lifetime's MainWindow, however it is reached; not DialogHost.MainWindow, not the type Views.MainWindow.
    [GeneratedRegex(@"(?<!\bDialogHost|\bViews)\.MainWindow\b")]
    private static partial Regex MainWindowLookup();

    [GeneratedRegex(@"\bEnvironment\.GetCommandLineArgs\b|""--(?:no-engine|hidden|gallery|config-folder)""|""AUGRAM_NO_ENGINE""|\bdesktop\.Args\b")]
    private static partial Regex LaunchSwitch();

    [GeneratedRegex(@"\bResources\s*\[[^\]]*\]\s*=(?!=)|ComponentPorts\.ResourceKey|""Augram\.ComponentPorts""|TryFindResource\(\s*""Augram\.")]
    private static partial Regex PortResource();

    [GeneratedRegex(@"\.(?:Try)?Add(?:Keyed)?(?:Singleton|Transient|Scoped)\b")]
    private static partial Regex Registration();

    [GeneratedRegex(@"\.(?:Try)?Add(?:Keyed)?Singleton\s*<\s*(?:IConfirmPresenter|IFormDialogPresenter)\b|\bnew\s+(?:ConfirmPresenter|FormDialogPresenter)\s*\(|\bConfirmPresenter\.Alone\(")]
    private static partial Regex PresenterRegistration();

    [GeneratedRegex(@"\.(?:Try)?Add(?:Keyed)?Singleton\s*<\s*(?:ConfigSession|SettingsStore|GestureLibrary|MappingStore)\b|\bnew\s+ConfigSession\s*\(|GetRequiredService<ConfigSession>\(\)\.(?:Settings|Gestures|Mapping)\b")]
    private static partial Regex StoreRegistration();

    [GeneratedRegex(@"\.TryAdd(?:Keyed)?(?:Singleton|Transient|Scoped|Enumerable)?\s*[<(]")]
    private static partial Regex TryAdd();

    [GeneratedRegex(@"\bAugram\.Platform\.(?:Windows|MacOS)\b")]
    private static partial Regex PlatformNamespace();

    [GeneratedRegex(@"\bnew\s+NavEntry\s*\(")]
    private static partial Regex NewNavEntry();

    [GeneratedRegex(@"\bAppearanceLink\.Follow\(|\bGet(?:Required)?Service<\s*MainWindow\s*>|\bnew\s+MainWindow\s*\(")]
    private static partial Regex MainWindowBuild();

    [GeneratedRegex(@"\.Activate\(\s*\)")]
    private static partial Regex Activate();

    [GeneratedRegex(@"\bNamedPipe(?:Server|Client)Stream\b|\bNamedWaitHandleOptions\b|\bnew\s+Mutex\s*\(|\bMutex\.(?:TryOpenExisting|OpenExisting)\b")]
    private static partial Regex PipeOrMutex();

    [GeneratedRegex(@"\bIStartupRegistration\b")]
    private static partial Regex StartupRegistration();

    [GeneratedRegex(@"\bnew\s+AppTray\s*\(")]
    private static partial Regex NewAppTray();

    [GeneratedRegex(@"\.SubEntries\b")]
    private static partial Regex SubEntries();
}
