using Augram.App.ViewModels;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>The app identification form app groups and ignored apps share: every matcher field round-trips, and syncing leaves text being typed alone.</summary>
public sealed class AppMatcherEditViewModelTests
{
    private static readonly AppMatcher Desktop = new()
    {
        WindowsProcessNames = ["explorer.exe"],
        MacProcessNames = ["Finder"],
        ProcessPath = @"^C:\\Windows\\.+$",
        ProcessPathIsRegex = true,
        Title = "Program Manager",
        ClassChain = ["Progman|WorkerW", "SHELLDLL_DefView"],
        IgnoreWhenFullScreen = true,
    };

    [Fact]
    public void EveryMatcherFieldRoundTrips()
    {
        var edit = New();

        edit.SyncFrom(Desktop);
        var matcher = edit.ToMatcher();

        Assert.Equal("Progman|WorkerW, SHELLDLL_DefView", edit.WindowClasses);
        Assert.Equal(Desktop.WindowsProcessNames, matcher.WindowsProcessNames);
        Assert.Equal(Desktop.MacProcessNames, matcher.MacProcessNames);
        Assert.Equal(Desktop.ClassChain, matcher.ClassChain);
        Assert.Equal((Desktop.ProcessPath, Desktop.ProcessPathIsRegex, Desktop.Title, Desktop.TitleIsRegex, Desktop.IgnoreWhenFullScreen), (matcher.ProcessPath, matcher.ProcessPathIsRegex, matcher.Title, matcher.TitleIsRegex, matcher.IgnoreWhenFullScreen));
    }

    [Fact]
    public void BlankTextIsNoValue_AndSyncingKeepsATrailingCommaBeingTyped()
    {
        var edit = New();
        edit.WindowsNames = "chrome.exe, ";
        edit.WindowTitle = "   ";
        edit.ProcessPath = " C:\\Tools\\x.exe ";

        var matcher = edit.ToMatcher();
        Assert.Equal(["chrome.exe"], matcher.WindowsProcessNames);
        Assert.Null(matcher.Title);
        Assert.Equal("C:\\Tools\\x.exe", matcher.ProcessPath);

        edit.SyncFrom(matcher);
        Assert.Equal("chrome.exe, ", edit.WindowsNames);
    }

    [Fact]
    public void TheGuessFollowsWhereTheHostIsUsed_InTheHostsWords()
    {
        var usedOn = PlatformSet.All;
        var edit = new AppMatcherEditViewModel(() => usedOn, "advice", "none needed");
        var raised = new List<string?>();
        edit.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.Equal("Type the executable's name for at least one platform.", edit.GuessText);
        edit.WindowsNames = "vmware.exe";
        Assert.Equal("macOS: no guess; advice", edit.GuessText);

        usedOn = PlatformSet.Windows;
        edit.UsedOnChanged();
        Assert.Equal("none needed", edit.GuessText);
        Assert.Equal(2, raised.Count(name => name == nameof(AppMatcherEditViewModel.GuessText)));
    }

    private static AppMatcherEditViewModel New() => new(() => PlatformSet.All, "advice", "none needed");
}
