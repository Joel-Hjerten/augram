using System.ComponentModel;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// What a window finder's pick puts into the shared identification form (app groups and ignored apps): "Identify window" adds
/// this platform's executable once and offers the rest with Use, a field's magnifier fills that field (path and title exact,
/// their regex toggle off, as one change), and a window without the value fills nothing. View model only; the rendered form
/// and the host's undo are in <see cref="AppMatcherFinderFormTests"/>.
/// </summary>
public sealed class AppMatcherFinderTests
{
    internal static readonly WindowIdentity Chrome = FakeWindowSystem.Window(
        "chrome.exe",
        "Google Chrome",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        ["Chrome_RenderWidgetHostHWND", "Chrome_WidgetWin_1"]) with
    {
        Levels = new WindowLevels(null, "Chrome_RenderWidgetHostHWND", "Google Chrome", "Chrome_WidgetWin_1", "Google Chrome", "Chrome_WidgetWin_1", "Google Chrome", "Chrome_WidgetWin_1"),
    };

    [Fact]
    public void TheExecutableJoinsThisPlatformsList_OnceInAnyCase()
    {
        var edit = New(HostPlatform.Windows);
        edit.WindowsNames = "msedge.exe";

        Assert.True(edit.TakeExecutable(Chrome));
        Assert.Equal("msedge.exe, chrome.exe", edit.WindowsNames);
        Assert.Equal(string.Empty, edit.MacNames);

        var raised = Raised(edit);
        Assert.False(edit.TakeExecutable(Chrome with { ProcessName = "CHROME.EXE" }));
        Assert.Equal("msedge.exe, chrome.exe", edit.WindowsNames);
        Assert.Empty(raised);
    }

    /// <summary>On a Mac the path and title magnifiers fill the macOS fields, never the Windows ones.</summary>
    [Fact]
    public void OnAMac_PathAndTitleGoToTheMacFields()
    {
        var edit = New(HostPlatform.MacOS);
        edit.ProcessPath = @"C:\keep.exe";
        var mac = FakeWindowSystem.Window("Google Chrome", "New Tab", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome");

        edit.TakePath(mac);
        edit.TakeTitle(mac);

        Assert.Equal(("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", "New Tab"), (edit.MacProcessPath, edit.MacWindowTitle));
        Assert.Equal((@"C:\keep.exe", string.Empty), (edit.ProcessPath, edit.WindowTitle));
        var matcher = edit.ToMatcher();
        Assert.Equal(("New Tab", false), matcher.TitleFor(HostPlatform.MacOS));
    }

    [Fact]
    public void OnAMac_APickedWindowGoesToTheMacList()
    {
        var edit = New(HostPlatform.MacOS);
        edit.WindowsNames = "chrome.exe";

        Assert.True(edit.TakeExecutable(FakeWindowSystem.Window("Google Chrome", "New Tab", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome")));

        Assert.Equal("Google Chrome", edit.MacNames);
        Assert.Equal("chrome.exe", edit.WindowsNames);
        Assert.Equal(["Google Chrome"], edit.ToMatcher().MacProcessNames);
    }

    [Fact]
    public void PathAndTitleFillExactly_TheirRegexOff_AsOneChange()
    {
        var edit = New(HostPlatform.Windows);
        edit.ProcessPath = @"^C:\\Program Files \(x86\)\\Steam\\steamapps\\common\\.+$";
        edit.PathIsRegex = true;
        edit.WindowTitle = "^Chrome";
        edit.TitleIsRegex = true;
        var raised = Raised(edit);

        edit.TakePath(Chrome);
        Assert.Equal((Chrome.ProcessPath, false), (edit.ProcessPath, edit.PathIsRegex));
        Assert.Equal([string.Empty], raised);

        edit.TakeTitle(Chrome);
        Assert.Equal(("Google Chrome", false), (edit.WindowTitle, edit.TitleIsRegex));
        Assert.Equal([string.Empty, string.Empty], raised);

        var matcher = edit.ToMatcher();
        Assert.Equal((Chrome.ProcessPath, false, "Google Chrome", false), (matcher.ProcessPath, matcher.ProcessPathIsRegex, matcher.Title, matcher.TitleIsRegex));
    }

    /// <summary>A per-window field's magnifier fills that field from its own window, exactly, its toggle off, as one change.</summary>
    [Fact]
    public void AWindowFieldFillsFromItsOwnWindow_ItsRegexOff_AsOneChange()
    {
        var edit = New(HostPlatform.Windows);
        edit.ControlClass = "^Chrome";
        edit.ControlClassIsRegex = true;
        var raised = Raised(edit);

        edit.TakeWindowField(Field(edit, "Control class"), Chrome);
        edit.TakeWindowField(Field(edit, "Root title"), Chrome);

        Assert.Equal(("Chrome_RenderWidgetHostHWND", false), (edit.ControlClass, edit.ControlClassIsRegex));
        Assert.Equal("Google Chrome", edit.RootTitle);
        Assert.Equal([string.Empty, string.Empty], raised);
        var matcher = edit.ToMatcher();
        Assert.Equal(("Chrome_RenderWidgetHostHWND", false, "Google Chrome"), (matcher.ControlClass, matcher.ControlClassIsRegex, matcher.RootTitle));
    }

    [Fact]
    public void EveryWindowFieldRoundTripsThroughTheForm()
    {
        var stored = new AppMatcher
        {
            WindowsProcessNames = [@"Spine(?:-1)?\.exe"],
            WindowsProcessNamesAreRegex = true,
            MacProcessNames = ["^Spine"],
            MacProcessNamesAreRegex = true,
            RootTitle = "r",
            ParentTitle = "p",
            ParentTitleIsRegex = true,
            ControlTitle = "c",
            OwnerClass = "oc",
            OwnerClassIsRegex = true,
            RootClass = "rc",
            ParentClass = "pc",
            ControlClass = "cc",
            ControlClassIsRegex = true,
        };
        var edit = New(HostPlatform.Windows);

        edit.SyncFrom(stored);
        var back = edit.ToMatcher();

        Assert.Equal(stored with { WindowsProcessNames = [], MacProcessNames = [] }, back with { WindowsProcessNames = [], MacProcessNames = [] });
        Assert.Equal(stored.WindowsProcessNames, back.WindowsProcessNames);
        Assert.Equal(stored.MacProcessNames, back.MacProcessNames);
    }

    [Fact]
    public void AWindowWithoutTheValue_FillsNothing()
    {
        var edit = New(HostPlatform.MacOS);
        edit.ProcessPath = "keep";
        edit.WindowTitle = "keep";
        edit.RootClass = "keep";
        var raised = Raised(edit);
        var bare = FakeWindowSystem.Window("Finder");

        edit.TakePath(bare);
        edit.TakeTitle(bare);
        edit.TakeWindowField(Field(edit, "Root class"), bare);

        Assert.Empty(raised);
        Assert.Equal(("keep", "keep", "keep"), (edit.ProcessPath, edit.WindowTitle, edit.RootClass));
    }

    /// <summary>The Windows | macOS switch is view state: flipping it raises nothing on the form, so the host applies nothing.</summary>
    [Fact]
    public void TheFormOpensOnThisMachinesPlatform_AndFlippingIsNoEdit()
    {
        var edit = New(HostPlatform.MacOS);
        Assert.Equal(HostPlatform.MacOS, edit.View.Shown);
        var raised = Raised(edit);

        edit.View.Shown = HostPlatform.Windows;

        Assert.Empty(raised);
        Assert.True(edit.View.ShowsWindows);
    }

    private static AppMatcherEditViewModel.WindowField Field(AppMatcherEditViewModel edit, string label) => edit.WindowFields.Single(field => field.Label == label);

    internal static AppMatcherEditViewModel New(HostPlatform platform) => new(() => PlatformSet.All, "advice", "none needed") { Platform = platform };

    private static List<string?> Raised(INotifyPropertyChanged source)
    {
        var raised = new List<string?>();
        source.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }
}
