using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// F8 per platform (Joel, 2026-10-07): executable names per platform with the known-app guess, "Use on" Windows and
/// macOS, and how both are written to the file.
/// </summary>
public sealed class PlatformMatchingTests
{
    private const HostPlatform Windows = HostPlatform.Windows;
    private const HostPlatform Mac = HostPlatform.MacOS;

    [Fact]
    public void EachPlatformMatchesOnItsOwnNames()
    {
        var chrome = new AppMatcher { WindowsProcessNames = ["chrome.exe"], MacProcessNames = ["Google Chrome", "Chromium"] };

        Assert.True(chrome.Matches(Window("chrome.exe"), Windows));
        Assert.False(chrome.Matches(Window("Google Chrome"), Windows));
        Assert.True(chrome.Matches(Window("Chromium"), Mac));
        Assert.False(chrome.Matches(Window("chrome.exe"), Mac));
    }

    [Fact]
    public void AnEmptyListMatchesOnTheKnownAppGuess_BothWays()
    {
        var windowsOnly = new AppMatcher { WindowsProcessNames = ["chrome.exe", "msedge.exe"] };
        var macOnly = new AppMatcher { MacProcessNames = ["Finder"] };

        Assert.True(windowsOnly.Matches(Window("Google Chrome"), Mac));
        Assert.True(windowsOnly.Matches(Window("microsoft edge"), Mac));
        Assert.True(windowsOnly.IsGuessedOn(Mac));
        Assert.Equal(["Google Chrome", "Microsoft Edge"], windowsOnly.EffectiveProcessNames(Mac));
        Assert.True(macOnly.Matches(Window("explorer.exe"), Windows));
        Assert.False(macOnly.IsGuessedOn(Mac));
    }

    [Fact]
    public void NamesWithNoGuessForThisPlatformMatchNothingHere_NeverEverything()
    {
        var game = new AppMatcher { WindowsProcessNames = ["ff7rebirth_.exe"] };

        Assert.Empty(game.EffectiveProcessNames(Mac));
        Assert.False(game.Matches(Window("Safari"), Mac));
        Assert.False(game.Matches(Window("ff7rebirth_.exe"), Mac));
        Assert.True(game.Matches(Window("ff7rebirth_.exe"), Windows));
    }

    [Fact]
    public void WithoutAnyNamesTheOtherFieldsDecideOnEveryPlatform()
    {
        var byTitle = new AppMatcher { Title = "Inbox" };

        Assert.True(byTitle.Matches(Window("anything", title: "Inbox"), Windows));
        Assert.True(byTitle.Matches(Window("anything", title: "Inbox"), Mac));
    }

    [Fact]
    public void KnownApps_GuessesBothWays_CaseInsensitive_WithoutDuplicates_AndNothingForUnknownApps()
    {
        Assert.Equal(["Google Chrome"], KnownApps.Guess(["CHROME.EXE", "chrome.exe"], Mac));
        Assert.Equal(["chrome.exe", "explorer.exe"], KnownApps.Guess(["google chrome", "Finder"], Windows));
        Assert.Empty(KnownApps.Guess(["Photoshop.exe", "game.exe"], Mac));
    }

    [Fact]
    public void AGroupNotUsedHereNeverMatchesHere_TheGlobalCommandFiresInstead()
    {
        var gameOnPc = NewGroup("Game", ByProcess("game.exe"), NewCommand("Game up", Up)) with { UseOn = PlatformSet.Windows };
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal(NewCommand("Global up", Up)), gameOnPc], []));

        Assert.Equal("Game up", CommandResolver.Resolve(mapping, Window("game.exe"), Trigger.ForGesture(Up), Windows).Command!.Name);
        Assert.Null(CommandResolver.FindGroup(mapping, Window("game.exe"), Mac));
        Assert.Equal("Global up", CommandResolver.Resolve(mapping, Window("game.exe"), Trigger.ForGesture(Up), Mac).Command!.Name);
        Assert.True(mapping.Global.IsUsedOn(Mac));
    }

    [Fact]
    public void AGroupMustBeUsedSomewhere_AndGlobalIsAlwaysEverywhere()
    {
        var nowhere = NewGroup("Nowhere") with { UseOn = PlatformSet.None };

        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(new MappingDocument([NewGlobal(), nowhere], [])));
        Assert.Equal("Use 'Nowhere' on at least one platform.", ex.Message);
        Assert.Equal(PlatformSet.All, MappingRules.Normalised(AppGroup.EmptyGlobal with { UseOn = PlatformSet.MacOS }).UseOn);
    }

    [Fact]
    public void TheFileWritesUseOnAndMacNamesOnlyWhenSet_SoExistingGroupsReadAndWriteAsBefore()
    {
        var plain = NewGroup("Chrome", ByProcess("chrome.exe"));
        var pcOnly = NewGroup("Game", new AppMatcher { WindowsProcessNames = ["game.exe"], MacProcessNames = ["Game"] }) with { UseOn = PlatformSet.Windows };
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal(), plain, pcOnly], []));

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json).Mapping;

        Assert.Equal(1, Count(json, "\"useOn\""));
        Assert.Contains("\"useOn\":[\"windows\"]", string.Concat(json.Where(c => !char.IsWhiteSpace(c))), StringComparison.Ordinal);
        Assert.Equal(1, Count(json, "\"macProcessNames\""));
        Assert.Equal(PlatformSet.All, back.Groups.Single(group => group.Name == "Chrome").UseOn);
        var game = back.Groups.Single(group => group.Name == "Game");
        Assert.Equal(PlatformSet.Windows, game.UseOn);
        Assert.Equal(["Game"], game.Matcher!.MacProcessNames);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = back }));
    }

    private static int Count(string text, string part) => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;
}
