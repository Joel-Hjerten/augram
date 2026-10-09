using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Xunit;

namespace Augram.Core.Tests.Mapping;

public sealed class AppMatcherTests
{
    [Theory]
    [InlineData("chrome.exe", true)]
    [InlineData("CHROME.EXE", true)]
    [InlineData("msedge.exe", true)]
    [InlineData("firefox.exe", false)]
    public void ProcessNamesMatchAnyOfIgnoringCase(string processName, bool expected)
    {
        var matcher = MappingFixtures.ByProcess("Chrome.exe", " msedge.exe ");

        Assert.Equal(expected, matcher.Matches(MappingFixtures.Window(processName), HostPlatform.Windows));
    }

    [Fact]
    public void NothingSpecifiedMatchesNothing()
    {
        Assert.True(AppMatcher.Empty.IsEmpty);
        Assert.False(AppMatcher.Empty.Matches(MappingFixtures.Window(), HostPlatform.Windows));

        var blanks = new AppMatcher { WindowsProcessNames = ["", "  "], ClassChain = [" "], Title = " ", IgnoreWhenFullScreen = true };
        Assert.True(blanks.IsEmpty);
        Assert.False(blanks.Matches(MappingFixtures.Window(), HostPlatform.Windows));
    }

    [Fact]
    public void PathIsExactIgnoringCaseUnlessRegex()
    {
        var exact = new AppMatcher { ProcessPath = @"C:\Apps\Foo.exe" };
        Assert.True(exact.Matches(MappingFixtures.Window(path: @"c:\apps\foo.exe"), HostPlatform.Windows));
        Assert.False(exact.Matches(MappingFixtures.Window(path: @"C:\Apps\Foo.exe.bak"), HostPlatform.Windows));
        Assert.False(exact.Matches(MappingFixtures.Window(path: null), HostPlatform.Windows));

        var steam = new AppMatcher { ProcessPath = @"^C:\\Program Files \(x86\)\\Steam\\steamapps\\common\\.+$", ProcessPathIsRegex = true };
        Assert.True(steam.Matches(MappingFixtures.Window(path: @"c:\program files (x86)\steam\steamapps\common\Game\game.exe"), HostPlatform.Windows));
        Assert.False(steam.Matches(MappingFixtures.Window(path: @"C:\Games\game.exe"), HostPlatform.Windows));
    }

    [Fact]
    public void TitleRegexFindsASubstringWhereExactDoesNot()
    {
        var exact = new AppMatcher { Title = "Chimera" };
        Assert.True(exact.Matches(MappingFixtures.Window(title: "chimera"), HostPlatform.Windows));
        Assert.False(exact.Matches(MappingFixtures.Window(title: "Chimera - Project"), HostPlatform.Windows));
        Assert.False(exact.Matches(MappingFixtures.Window(title: null), HostPlatform.Windows));

        var regex = new AppMatcher { Title = "chimera", TitleIsRegex = true };
        Assert.True(regex.Matches(MappingFixtures.Window(title: "Chimera - Project"), HostPlatform.Windows));
    }

    [Fact]
    public void EverySpecifiedFieldMustMatch()
    {
        var matcher = new AppMatcher { WindowsProcessNames = ["chrome.exe"], Title = "Inbox" };

        Assert.True(matcher.Matches(MappingFixtures.Window("chrome.exe", title: "Inbox"), HostPlatform.Windows));
        Assert.False(matcher.Matches(MappingFixtures.Window("chrome.exe", title: "Other"), HostPlatform.Windows));
        Assert.False(matcher.Matches(MappingFixtures.Window("firefox.exe", title: "Inbox"), HostPlatform.Windows));
    }

    [Fact]
    public void ClassChainIsASubsetAndAnEntryMayListAlternatives()
    {
        var desktop = new AppMatcher { ClassChain = ["Progman|WorkerW", "SHELLDLL_DefView", "SysListView32"] };

        Assert.True(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: ["SysListView32", "shelldll_defview", "WorkerW"]), HostPlatform.Windows));
        Assert.True(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: ["SysListView32", "SHELLDLL_DefView", "Progman", "Extra"]), HostPlatform.Windows));
        Assert.False(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: ["SHELLDLL_DefView", "Progman"]), HostPlatform.Windows));
        Assert.False(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: []), HostPlatform.Windows));
    }

    [Fact]
    public void FullScreenWindowsAreExcludedOnlyWhenAsked()
    {
        var plain = MappingFixtures.ByProcess("game.exe");
        var windowedOnly = plain with { IgnoreWhenFullScreen = true };
        var fullScreen = MappingFixtures.Window("game.exe", fullScreen: true);

        Assert.True(plain.Matches(fullScreen, HostPlatform.Windows));
        Assert.False(windowedOnly.Matches(fullScreen, HostPlatform.Windows));
        Assert.True(windowedOnly.Matches(MappingFixtures.Window("game.exe"), HostPlatform.Windows));
    }

    [Fact]
    public void AnInvalidRegexIsNoMatchHereAndARuleViolationAtSaveTime()
    {
        var matcher = new AppMatcher { Title = "(", TitleIsRegex = true };

        Assert.False(matcher.Matches(MappingFixtures.Window(title: "("), HostPlatform.Windows));
        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.EnsureValid(matcher));
        Assert.Contains("title pattern '('", ex.Message, StringComparison.Ordinal);

        MappingRules.EnsureValid(matcher with { TitleIsRegex = false });
        MappingRules.EnsureValid(new AppMatcher { ProcessPath = @"\.exe$", ProcessPathIsRegex = true });
    }

    [Fact]
    public void MatchersAreValuesSoAStoreSnapshotCanBeCompared()
    {
        Assert.Equal(new AppMatcher { Title = "x", TitleIsRegex = true }, new AppMatcher { Title = "x", TitleIsRegex = true });
        Assert.NotEqual(new AppMatcher { Title = "x" }, new AppMatcher { Title = "y" });
    }

    /// <summary>Regex executable names (Joel, 2026-10-09): SP.net's <c>Spine(?:-1)?\.exe</c> and <c>PotPlayerMini.*.exe</c>.</summary>
    [Theory]
    [InlineData("Spine.exe", true)]
    [InlineData("spine-1.exe", true)]
    [InlineData("PotPlayerMini64.exe", true)]
    [InlineData("Spine-2.exe", false)]
    [InlineData("chrome.exe", false)]
    public void ProcessNamesArePatternsWhenTheirToggleIsOn(string processName, bool expected)
    {
        var matcher = new AppMatcher { WindowsProcessNames = [@"^Spine(?:-1)?\.exe$", "PotPlayerMini.*.exe"], WindowsProcessNamesAreRegex = true };

        Assert.Equal(expected, matcher.Matches(MappingFixtures.Window(processName), HostPlatform.Windows));
        Assert.False(new AppMatcher { WindowsProcessNames = ["PotPlayerMini.*.exe"] }.Matches(MappingFixtures.Window("PotPlayerMini64.exe"), HostPlatform.Windows));
    }

    [Fact]
    public void APatternListGivesTheOtherPlatformNoGuess()
    {
        var plain = new AppMatcher { WindowsProcessNames = ["chrome.exe"] };
        var patterns = plain with { WindowsProcessNamesAreRegex = true };

        Assert.NotEmpty(plain.EffectiveProcessNames(HostPlatform.MacOS));
        Assert.Empty(patterns.EffectiveProcessNames(HostPlatform.MacOS));
        Assert.False(patterns.Matches(MappingFixtures.Window("Google Chrome"), HostPlatform.MacOS));
    }

    [Fact]
    public void TheMacListsToggleAppliesOnlyToTheMacList()
    {
        var matcher = new AppMatcher { MacProcessNames = ["^Adobe Photoshop"], MacProcessNamesAreRegex = true };

        Assert.True(matcher.Matches(MappingFixtures.Window("Adobe Photoshop 2026"), HostPlatform.MacOS));
        Assert.False((matcher with { MacProcessNamesAreRegex = false }).Matches(MappingFixtures.Window("Adobe Photoshop 2026"), HostPlatform.MacOS));
    }

    /// <summary>Each SP.net per-window field reads its own window's caption or class, exact unless its toggle is on.</summary>
    [Fact]
    public void EachWindowFieldMatchesItsOwnWindow()
    {
        var levels = new WindowLevels("FolderView", "SysListView32", null, "SHELLDLL_DefView", "Program Manager", "Progman", "Program Manager", "Progman");
        var desktop = MappingFixtures.Window("explorer.exe", levels: levels);

        Assert.True(new AppMatcher { ControlClass = "syslistview32", ParentClass = "SHELLDLL_DefView", RootClass = "Progman" }.Matches(desktop, HostPlatform.Windows));
        Assert.True(new AppMatcher { OwnerClass = "^(Progman|WorkerW)$", OwnerClassIsRegex = true }.Matches(desktop, HostPlatform.Windows));
        Assert.True(new AppMatcher { RootTitle = "Program Manager", ControlTitle = "folderview" }.Matches(desktop, HostPlatform.Windows));
        Assert.False(new AppMatcher { RootClass = "SysListView32" }.Matches(desktop, HostPlatform.Windows));
        Assert.False(new AppMatcher { ControlClass = "SysList" }.Matches(desktop, HostPlatform.Windows));
        Assert.True(new AppMatcher { ControlClass = "SysList", ControlClassIsRegex = true }.Matches(desktop, HostPlatform.Windows));

        // A window without the value (no parent title here, or any field on macOS) does not match a field that asks for one.
        Assert.False(new AppMatcher { ParentTitle = "x" }.Matches(desktop, HostPlatform.Windows));
        Assert.False(new AppMatcher { RootClass = "Progman" }.Matches(MappingFixtures.Window("Finder"), HostPlatform.MacOS));
    }

    [Fact]
    public void AWindowFieldAloneIsNotEmpty()
    {
        Assert.False(new AppMatcher { ControlClass = "Edit" }.IsEmpty);
        Assert.True(new AppMatcher { ControlClass = " ", RootTitle = "" }.IsEmpty);
    }

    /// <summary>Each platform matches on its own path only (2026-10-09: a Windows path synced to the Mac made Chrome match nothing there).</summary>
    [Fact]
    public void EachPlatformLooksAtItsOwnPathOnly()
    {
        var chrome = new AppMatcher
        {
            WindowsProcessNames = ["chrome.exe"],
            MacProcessNames = ["Google Chrome"],
            ProcessPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        };
        var mac = MappingFixtures.Window("Google Chrome", path: "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome");

        Assert.True(chrome.Matches(mac, HostPlatform.MacOS));
        Assert.False((chrome with { MacProcessPath = "/Applications/Other.app/Contents/MacOS/Other" }).Matches(mac, HostPlatform.MacOS));
        Assert.True((chrome with { MacProcessPath = "^/Applications/Google Chrome", MacProcessPathIsRegex = true }).Matches(mac, HostPlatform.MacOS));
        Assert.False(chrome.Matches(MappingFixtures.Window("chrome.exe", path: @"D:\chrome.exe"), HostPlatform.Windows));
        Assert.False(new AppMatcher { MacProcessPath = "/x" }.IsEmpty);
    }

    /// <summary>A matcher whose only field is the other platform's path matches nothing here, never everything (Joel's Steam games group).</summary>
    [Fact]
    public void OnlyTheOtherPlatformsPath_MatchesNothing()
    {
        var steam = new AppMatcher { ProcessPath = @"^C:\\Program Files \(x86\)\\Steam\\steamapps\\common\\.+$", ProcessPathIsRegex = true };

        Assert.False(steam.IsEmpty);
        Assert.True(steam.IsEmptyOn(HostPlatform.MacOS));
        Assert.False(steam.Matches(MappingFixtures.Window("Google Chrome", path: "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"), HostPlatform.MacOS));
        Assert.True(steam.Matches(MappingFixtures.Window("game.exe", path: @"C:\Program Files (x86)\Steam\steamapps\common\Game\game.exe"), HostPlatform.Windows));

        var macOnly = new AppMatcher { MacProcessPath = "^/Applications/Steam", MacProcessPathIsRegex = true };
        Assert.False(macOnly.Matches(MappingFixtures.Window("chrome.exe", path: @"C:\chrome.exe"), HostPlatform.Windows));
    }

    [Fact]
    public void EachPlatformLooksAtItsOwnTitleOnly()
    {
        var chimera = new AppMatcher { Title = "Chimera" };

        Assert.True(chimera.Matches(MappingFixtures.Window("chimera.exe", title: "Chimera"), HostPlatform.Windows));
        Assert.False(chimera.Matches(MappingFixtures.Window("Chimera", title: "Chimera"), HostPlatform.MacOS));
        Assert.True((chimera with { MacTitle = "^Chim", MacTitleIsRegex = true }).Matches(MappingFixtures.Window("Chimera", title: "Chimera"), HostPlatform.MacOS));
    }
}
