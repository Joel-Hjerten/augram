using Augram.App.Components.MasterDetail;
using Augram.App.Components.SectionForm;
using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// The identification form rendered, its magnifiers dragged with simulated pointer events onto a <see cref="FakeWindowSystem"/>
/// window outside the test window: which fields have one, each pick filling its field, Use, and on the Ignored tab each pick
/// applied as one undo step through the host's normal edit path. No real pointer moves and no real window is read.
/// </summary>
public sealed class AppMatcherFinderFormTests
{
    private static readonly Point OverChrome = new(2000, 100);

    [AvaloniaFact]
    public void TheMagnifiersSitBeforeIdentifyWindow_ThisPlatformsExecutables_ThePath_TheTitle_AndOnWindowsEachWindowField()
    {
        var windows = Show(AppMatcherFinderTests.New(HostPlatform.Windows)).Rows;
        var mac = Show(AppMatcherFinderTests.New(HostPlatform.MacOS)).Rows;

        Assert.Equal(["Identify window", "Windows executables", "Windows executable path", "Window title", "Root title", "Parent title", "Control title", "Owner class", "Root class", "Parent class", "Control class"], FinderLabels(windows));
        Assert.Equal(["Identify window", "macOS executables", "macOS executable path", "Window title"], FinderLabels(mac));
        Assert.Equal(["Its path", "Its title", "Its root title", "Its parent title", "Its control title", "Its owner class", "Its root class", "Its parent class", "Its control class"], windows.Where(row => row.Accessory is Button).Select(row => row.Label));
    }

    [AvaloniaFact]
    public void EachFieldsMagnifier_FillsItsField_AndIdentifyOffersTheRestWithUse()
    {
        var edit = AppMatcherFinderTests.New(HostPlatform.Windows);
        var (window, rows) = Show(edit);

        Drag(window, Finder(rows, "Windows executables"));
        Drag(window, Finder(rows, "Window title"));
        Drag(window, Finder(rows, "Control class"));
        Assert.Equal(("chrome.exe", "Google Chrome", "Chrome_RenderWidgetHostHWND", string.Empty), (edit.WindowsNames, edit.WindowTitle, edit.ControlClass, edit.ProcessPath));
        Assert.False(Row(rows, "Its root class").IsVisible);
        Assert.False(Row(rows, "Its path").IsVisible);

        Drag(window, Finder(rows, "Identify window"));
        Assert.Equal("chrome.exe", edit.WindowsNames);
        Assert.Equal(AppMatcherFinderTests.Chrome.ProcessPath, ((TextBlock)Row(rows, "Its path").Editor!).Text);
        Assert.True(Row(rows, "Its path").IsVisible);
        Assert.StartsWith("chrome.exe · Google Chrome: chrome.exe is already", ((TextBlock)Row(rows, "Identify window").Editor!).Text, StringComparison.Ordinal);

        ((Button)Row(rows, "Its path").Accessory!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(AppMatcherFinderTests.Chrome.ProcessPath, edit.ProcessPath);
        Assert.True(Row(rows, "Its root class").IsVisible);
        Assert.False(Row(rows, "Its control title").IsVisible);
        ((Button)Row(rows, "Its root class").Accessory!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Chrome_WidgetWin_1", edit.RootClass);
        Assert.Equal(AppMatcherFinderTests.Chrome.ProcessPath, ((TextBox)Row(rows, "Windows executable path").Editor!).Text);
    }

    [AvaloniaFact]
    public void OnTheIgnoredTab_EachPickIsOneEditOfTheStore_AndUndoTakesItBack()
    {
        var steam = new IgnoredApp(GroupId.New(), "Steam games", IsActive: true, new AppMatcher { ProcessPath = @"^C:\\Steam\\.+$", ProcessPathIsRegex = true, MacProcessPath = "^/Games/", MacProcessPathIsRegex = true }, DisableEntirely: false);
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], [steam]));
        var vm = new IgnoredViewModel(store, new FakeFormDialogPresenter(), new FakeConfirmPresenter(), CommandsModule.CurrentPlatform);
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, vm.Items.Single()));
        var (window, rows) = Show(vm.Detail!);

        Drag(window, Finder(rows, "Identify window"));
        Assert.Equal(["chrome.exe"], Stored().ProcessNamesFor(CommandsModule.CurrentPlatform));

        Drag(window, Finder(rows, PathLabel));
        Assert.Equal((AppMatcherFinderTests.Chrome.ProcessPath, false), Stored().PathFor(CommandsModule.CurrentPlatform));

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Undo));
        Assert.Equal(steam.Matcher.PathFor(CommandsModule.CurrentPlatform), Stored().PathFor(CommandsModule.CurrentPlatform));
        Assert.Equal(["chrome.exe"], Stored().ProcessNamesFor(CommandsModule.CurrentPlatform));
        Assert.Equal(steam.Matcher.PathFor(CommandsModule.CurrentPlatform).Path ?? string.Empty, ((TextBox)Row(rows, PathLabel).Editor!).Text ?? string.Empty);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Undo));
        Assert.Empty(Stored().ProcessNamesFor(CommandsModule.CurrentPlatform));

        AppMatcher Stored() => store.FindIgnored(steam.Id)!.Matcher;
    }

    /// <summary>This platform's path field: the one the magnifier fills here.</summary>
    private static string PathLabel => CommandsModule.CurrentPlatform == HostPlatform.MacOS ? "macOS executable path" : "Windows executable path";

    private static (Window Window, List<FieldRow> Rows) Show(AppMatcherEditViewModel edit) => Show(new FormScreen("Identification", edit.Sections()));

    private static (Window Window, List<FieldRow> Rows) Show(FormScreen screen)
    {
        var form = new SectionForm { Screen = screen };
        var window = new Window { Width = 900, Height = 3200, Content = form };
        window.Show();
        var at = window.PointToScreen(OverChrome);
        window.Resources[WindowFinder.WindowSystemResourceKey] = new FakeWindowSystem().Around(at.X, at.Y, AppMatcherFinderTests.Chrome);
        return (window, form.GetVisualDescendants().OfType<FieldRow>().ToList());
    }

    private static IEnumerable<string> FinderLabels(IEnumerable<FieldRow> rows) => rows.Where(row => row.Accessory is WindowFinder).Select(row => row.Label);

    private static FieldRow Row(IEnumerable<FieldRow> rows, string label) => rows.Single(row => row.Label == label);

    private static WindowFinder Finder(IEnumerable<FieldRow> rows, string label) => (WindowFinder)Row(rows, label).Accessory!;

    /// <summary>Left press on the magnifier, a move onto Chrome outside the window, release there.</summary>
    private static void Drag(Window window, WindowFinder finder)
    {
        window.MouseDown(finder.TranslatePoint(new Point(finder.Bounds.Width / 2, finder.Bounds.Height / 2), window)!.Value, MouseButton.Left);
        window.MouseMove(OverChrome);
        window.MouseUp(OverChrome, MouseButton.Left);
    }
}
