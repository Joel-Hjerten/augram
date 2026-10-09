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

    /// <summary>SP.net's rows (Joel, 2026-10-09): every field a text box, a magnifier on this machine's side, Use Regex; the switch flips the rows.</summary>
    [AvaloniaFact]
    public void EachRowHasItsMagnifierAndUseRegex_AndTheSwitchFlipsBetweenPlatforms()
    {
        var edit = AppMatcherFinderTests.New(HostPlatform.Windows);
        var rows = Show(edit).Rows;

        string[] windowsRows = ["Executable", "Executable path", "Window title", "Root title", "Parent title", "Control title", "Owner class", "Root class", "Parent class", "Control class"];
        Assert.Equal(windowsRows, FinderLabels(rows));
        Assert.All(windowsRows, label => Assert.Equal(PatternField.UseRegexCaption, RegexBox(rows, label).Content));
        Assert.False(Row(rows, "Window classes (older)").IsVisible);

        edit.View.Shown = HostPlatform.MacOS;

        Assert.Equal(["Executable", "Executable path", "Window title", "Not when full screen"], rows.Where(row => row.IsVisible).Select(row => row.Label).Skip(1));
        Assert.Empty(FinderLabels(rows));
    }

    /// <summary>A pattern line fills the editor column and stays in it, also in a narrow window (Joel, 2026-10-09: it spilled over the labels).</summary>
    [AvaloniaTheory]
    [InlineData(900, 400)]
    [InlineData(460, 25)]
    public void APatternLineStaysRightOfTheLabels_AndInsideTheRow(double width, double minimum)
    {
        var edit = AppMatcherFinderTests.New(HostPlatform.Windows);
        edit.ProcessPath = new string('x', 300);
        var form = new SectionForm { Screen = new FormScreen("Identification", edit.Sections()) };
        var window = new Window { Width = width, Height = 3200, Content = form };
        window.Show();

        foreach (var label in new[] { "Executable", "Executable path", "Window title", "Control class" })
        {
            var row = form.GetVisualDescendants().OfType<FieldRow>().Single(candidate => candidate.Label == label);
            var line = (Control)row.Editor!;
            // The text box and the check box themselves: the line kept its place while its box overflowed left inside it.
            var box = line.GetVisualDescendants().OfType<TextBox>().First();
            var regex = line.GetVisualDescendants().OfType<CheckBox>().Single();
            var left = box.TranslatePoint(new Point(0, 0), row)!.Value.X;
            var right = regex.TranslatePoint(new Point(regex.Bounds.Width, 0), row)!.Value.X;
            Assert.True(left >= 220, $"{label}'s box starts at {left}, over the label column");
            Assert.True(right <= row.Bounds.Width + 0.5, $"{label}'s Use Regex ends past the row");
            Assert.True(box.Bounds.Width > minimum, $"{label}'s box does not fill the column ({box.Bounds.Width})");
        }
    }

    [AvaloniaFact]
    public void TheSwitchShowsEachPlatformsOwnValues_AndTypingGoesToTheShownOne()
    {
        var edit = AppMatcherFinderTests.New(HostPlatform.MacOS);
        edit.SyncFrom(new AppMatcher { WindowsProcessNames = ["chrome.exe"], ProcessPath = @"C:\chrome.exe", MacProcessNames = ["Google Chrome"], MacProcessNamesAreRegex = true });
        var rows = Show(edit).Rows;

        Assert.Equal("Google Chrome", Box(rows, "Executable").Text);
        Assert.True(RegexBox(rows, "Executable").IsChecked);
        Assert.Equal(string.Empty, Box(rows, "Executable path").Text ?? string.Empty);

        edit.View.Shown = HostPlatform.Windows;
        Assert.Equal("chrome.exe", Box(rows, "Executable").Text);
        Assert.False(RegexBox(rows, "Executable").IsChecked);
        Assert.Equal(@"C:\chrome.exe", Box(rows, "Executable path").Text);

        Box(rows, "Executable path").Text = string.Empty;
        Assert.Equal((string.Empty, string.Empty), (edit.ProcessPath, edit.MacProcessPath));
        Assert.Equal("Google Chrome", edit.MacNames);
    }

    [AvaloniaFact]
    public void AnEmptyExecutableBoxShowsTheGuess()
    {
        var edit = AppMatcherFinderTests.New(HostPlatform.MacOS);
        edit.WindowsNames = "chrome.exe";
        var rows = Show(edit).Rows;

        Assert.Equal("Google Chrome (guessed)", Box(rows, "Executable").Watermark);
    }

    [AvaloniaFact]
    public void EachFieldsMagnifier_FillsItsField()
    {
        var edit = AppMatcherFinderTests.New(HostPlatform.Windows);
        var (window, rows) = Show(edit);

        Drag(window, Finder(rows, "Executable"));
        Drag(window, Finder(rows, "Window title"));
        Drag(window, Finder(rows, "Control class"));
        Drag(window, Finder(rows, "Executable path"));

        Assert.Equal(("chrome.exe", "Google Chrome", "Chrome_RenderWidgetHostHWND"), (edit.WindowsNames, edit.WindowTitle, edit.ControlClass));
        Assert.Equal(AppMatcherFinderTests.Chrome.ProcessPath, edit.ProcessPath);
        Assert.Equal(AppMatcherFinderTests.Chrome.ProcessPath, Box(rows, "Executable path").Text);
    }

    [AvaloniaFact]
    public void OnTheIgnoredTab_EachPickIsOneEditOfTheStore_AndUndoTakesItBack()
    {
        var steam = new IgnoredApp(GroupId.New(), "Steam games", IsActive: true, new AppMatcher { ProcessPath = @"^C:\\Steam\\.+$", ProcessPathIsRegex = true, MacProcessPath = "^/Games/", MacProcessPathIsRegex = true }, DisableEntirely: false);
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], [steam]));
        var vm = new IgnoredViewModel(store, new FakeFormDialogPresenter(), new FakeConfirmPresenter(), CommandsModule.CurrentPlatform);
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, vm.Items.Single()));
        var (window, rows) = Show(vm.Detail!);

        Drag(window, Finder(rows, "Executable"));
        Assert.Equal(["chrome.exe"], Stored().ProcessNamesFor(CommandsModule.CurrentPlatform));

        Drag(window, Finder(rows, "Executable path"));
        Assert.Equal((AppMatcherFinderTests.Chrome.ProcessPath, false), Stored().PathFor(CommandsModule.CurrentPlatform));

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Undo));
        Assert.Equal(steam.Matcher.PathFor(CommandsModule.CurrentPlatform), Stored().PathFor(CommandsModule.CurrentPlatform));
        Assert.Equal(["chrome.exe"], Stored().ProcessNamesFor(CommandsModule.CurrentPlatform));
        Assert.Equal(steam.Matcher.PathFor(CommandsModule.CurrentPlatform).Path ?? string.Empty, Box(rows, "Executable path").Text ?? string.Empty);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Undo));
        Assert.Empty(Stored().ProcessNamesFor(CommandsModule.CurrentPlatform));

        AppMatcher Stored() => store.FindIgnored(steam.Id)!.Matcher;
    }

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

    /// <summary>The visible rows whose magnifier shows.</summary>
    private static IEnumerable<string> FinderLabels(IEnumerable<FieldRow> rows)
        => rows.Where(row => row.IsVisible && row.Editor is Control editor && editor.GetVisualDescendants().OfType<WindowFinder>().Any(finder => finder.IsVisible)).Select(row => row.Label);

    private static TextBox Box(IEnumerable<FieldRow> rows, string label) => ((Control)Row(rows, label).Editor!).GetVisualDescendants().OfType<TextBox>().First();

    private static CheckBox RegexBox(IEnumerable<FieldRow> rows, string label) => ((Control)Row(rows, label).Editor!).GetVisualDescendants().OfType<CheckBox>().Single();

    private static FieldRow Row(IEnumerable<FieldRow> rows, string label) => rows.Single(row => row.Label == label);

    private static WindowFinder Finder(IEnumerable<FieldRow> rows, string label) => ((Control)Row(rows, label).Editor!).GetVisualDescendants().OfType<WindowFinder>().Single();

    /// <summary>Left press on the magnifier, a move onto Chrome outside the window, release there.</summary>
    private static void Drag(Window window, WindowFinder finder)
    {
        window.MouseDown(finder.TranslatePoint(new Point(finder.Bounds.Width / 2, finder.Bounds.Height / 2), window)!.Value, MouseButton.Left);
        window.MouseMove(OverChrome);
        window.MouseUp(OverChrome, MouseButton.Left);
    }
}
