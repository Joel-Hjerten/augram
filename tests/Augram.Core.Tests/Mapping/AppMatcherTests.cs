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

        Assert.Equal(expected, matcher.Matches(MappingFixtures.Window(processName)));
    }

    [Fact]
    public void NothingSpecifiedMatchesNothing()
    {
        Assert.True(AppMatcher.Empty.IsEmpty);
        Assert.False(AppMatcher.Empty.Matches(MappingFixtures.Window()));

        var blanks = new AppMatcher { ProcessNames = ["", "  "], ClassChain = [" "], Title = " ", IgnoreWhenFullScreen = true };
        Assert.True(blanks.IsEmpty);
        Assert.False(blanks.Matches(MappingFixtures.Window()));
    }

    [Fact]
    public void PathIsExactIgnoringCaseUnlessRegex()
    {
        var exact = new AppMatcher { ProcessPath = @"C:\Apps\Foo.exe" };
        Assert.True(exact.Matches(MappingFixtures.Window(path: @"c:\apps\foo.exe")));
        Assert.False(exact.Matches(MappingFixtures.Window(path: @"C:\Apps\Foo.exe.bak")));
        Assert.False(exact.Matches(MappingFixtures.Window(path: null)));

        var steam = new AppMatcher { ProcessPath = @"^C:\\Program Files \(x86\)\\Steam\\steamapps\\common\\.+$", ProcessPathIsRegex = true };
        Assert.True(steam.Matches(MappingFixtures.Window(path: @"c:\program files (x86)\steam\steamapps\common\Game\game.exe")));
        Assert.False(steam.Matches(MappingFixtures.Window(path: @"C:\Games\game.exe")));
    }

    [Fact]
    public void TitleRegexFindsASubstringWhereExactDoesNot()
    {
        var exact = new AppMatcher { Title = "Chimera" };
        Assert.True(exact.Matches(MappingFixtures.Window(title: "chimera")));
        Assert.False(exact.Matches(MappingFixtures.Window(title: "Chimera - Project")));
        Assert.False(exact.Matches(MappingFixtures.Window(title: null)));

        var regex = new AppMatcher { Title = "chimera", TitleIsRegex = true };
        Assert.True(regex.Matches(MappingFixtures.Window(title: "Chimera - Project")));
    }

    [Fact]
    public void EverySpecifiedFieldMustMatch()
    {
        var matcher = new AppMatcher { ProcessNames = ["chrome.exe"], Title = "Inbox" };

        Assert.True(matcher.Matches(MappingFixtures.Window("chrome.exe", title: "Inbox")));
        Assert.False(matcher.Matches(MappingFixtures.Window("chrome.exe", title: "Other")));
        Assert.False(matcher.Matches(MappingFixtures.Window("firefox.exe", title: "Inbox")));
    }

    [Fact]
    public void ClassChainIsASubsetAndAnEntryMayListAlternatives()
    {
        var desktop = new AppMatcher { ClassChain = ["Progman|WorkerW", "SHELLDLL_DefView", "SysListView32"] };

        Assert.True(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: ["SysListView32", "shelldll_defview", "WorkerW"])));
        Assert.True(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: ["SysListView32", "SHELLDLL_DefView", "Progman", "Extra"])));
        Assert.False(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: ["SHELLDLL_DefView", "Progman"])));
        Assert.False(desktop.Matches(MappingFixtures.Window("explorer.exe", classChain: [])));
    }

    [Fact]
    public void FullScreenWindowsAreExcludedOnlyWhenAsked()
    {
        var plain = MappingFixtures.ByProcess("game.exe");
        var windowedOnly = plain with { IgnoreWhenFullScreen = true };
        var fullScreen = MappingFixtures.Window("game.exe", fullScreen: true);

        Assert.True(plain.Matches(fullScreen));
        Assert.False(windowedOnly.Matches(fullScreen));
        Assert.True(windowedOnly.Matches(MappingFixtures.Window("game.exe")));
    }

    [Fact]
    public void AnInvalidRegexIsNoMatchHereAndARuleViolationAtSaveTime()
    {
        var matcher = new AppMatcher { Title = "(", TitleIsRegex = true };

        Assert.False(matcher.Matches(MappingFixtures.Window(title: "(")));
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
}
