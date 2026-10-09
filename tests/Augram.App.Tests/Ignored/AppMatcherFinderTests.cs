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
        ["Chrome_RenderWidgetHostHWND", "Chrome_WidgetWin_1"]);

    [Fact]
    public void IdentifyAddsTheExecutableToThisPlatformsList_OnceInAnyCase()
    {
        var edit = New(HostPlatform.Windows);
        edit.WindowsNames = "msedge.exe";

        edit.IdentifyWindow(Chrome);
        Assert.Equal("msedge.exe, chrome.exe", edit.WindowsNames);
        Assert.Equal(string.Empty, edit.MacNames);
        Assert.Equal("chrome.exe · Google Chrome: added chrome.exe to the Windows executables.", edit.Identified.Summary);

        var raised = Raised(edit);
        edit.IdentifyWindow(Chrome with { ProcessName = "CHROME.EXE" });
        Assert.Equal("msedge.exe, chrome.exe", edit.WindowsNames);
        Assert.Empty(raised);
        Assert.Equal("CHROME.EXE · Google Chrome: CHROME.EXE is already in the Windows executables.", edit.Identified.Summary);
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

    [Fact]
    public void ClassesFillTheChain()
    {
        var edit = New(HostPlatform.Windows);
        edit.WindowClasses = "Progman|WorkerW";

        edit.TakeClasses(Chrome);

        Assert.Equal("Chrome_RenderWidgetHostHWND, Chrome_WidgetWin_1", edit.WindowClasses);
        Assert.Equal(Chrome.ClassChain, edit.ToMatcher().ClassChain);
    }

    [Fact]
    public void AWindowWithoutTheValue_FillsNothing()
    {
        var edit = New(HostPlatform.MacOS);
        edit.ProcessPath = "keep";
        edit.WindowTitle = "keep";
        edit.WindowClasses = "keep";
        var raised = Raised(edit);
        var bare = FakeWindowSystem.Window("Finder");

        edit.TakePath(bare);
        edit.TakeTitle(bare);
        edit.TakeClasses(bare);

        Assert.Empty(raised);
        Assert.Equal(("keep", "keep", "keep"), (edit.ProcessPath, edit.WindowTitle, edit.WindowClasses));
    }

    [Fact]
    public void TheIdentifiedWindowOffersItsOtherProperties_AndUseCopiesEachIntoItsField()
    {
        var edit = New(HostPlatform.Windows);
        Assert.Equal(IdentifiedWindowViewModel.NothingPicked, edit.Identified.Summary);
        Assert.False(edit.Identified.HasPath || edit.Identified.HasTitle || edit.Identified.HasClasses);
        edit.UseIdentifiedPath();
        Assert.Equal(string.Empty, edit.ProcessPath);

        edit.IdentifyWindow(Chrome);
        Assert.True(edit.Identified.HasPath && edit.Identified.HasTitle && edit.Identified.HasClasses);
        Assert.Equal((Chrome.ProcessPath, "Google Chrome", "Chrome_RenderWidgetHostHWND, Chrome_WidgetWin_1"), (edit.Identified.PathText, edit.Identified.TitleText, edit.Identified.ClassesText));
        Assert.Equal((string.Empty, string.Empty, string.Empty), (edit.ProcessPath, edit.WindowTitle, edit.WindowClasses));

        edit.UseIdentifiedPath();
        edit.UseIdentifiedTitle();
        edit.UseIdentifiedClasses();

        Assert.Equal((Chrome.ProcessPath, "Google Chrome", "Chrome_RenderWidgetHostHWND, Chrome_WidgetWin_1"), (edit.ProcessPath, edit.WindowTitle, edit.WindowClasses));
    }

    internal static AppMatcherEditViewModel New(HostPlatform platform) => new(() => PlatformSet.All, "advice", "none needed") { Platform = platform };

    private static List<string?> Raised(INotifyPropertyChanged source)
    {
        var raised = new List<string?>();
        source.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }
}
