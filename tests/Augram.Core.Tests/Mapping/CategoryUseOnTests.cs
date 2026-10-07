using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// "Use on" per category (Joel, 2026-10-08): a category's platforms apply to every command in it, on top of the group's and
/// the command's own (<see cref="AppGroup.IsCommandUsedOn"/>, the one place the AND is decided), and are written to the file
/// like a group's and a command's.
/// </summary>
public sealed class CategoryUseOnTests
{
    private const HostPlatform Windows = HostPlatform.Windows;
    private const HostPlatform Mac = HostPlatform.MacOS;

    private readonly List<string> _notices = [];

    [Fact]
    public void AGlobalCommandInAWindowsOnlyCategoryFiresOnWindowsAndNotOnTheMac()
    {
        var personal = NewCategory("Personal") with { UseOn = PlatformSet.Windows };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Show desktop", Up).In(personal)) with { Categories = [personal] }));

        var onWindows = CommandResolver.Resolve(mapping, Window("notepad.exe"), Trigger.ForGesture(Up), Windows);
        var onMac = CommandResolver.Resolve(mapping, Window("TextEdit"), Trigger.ForGesture(Up), Mac);

        Assert.Equal("Show desktop", onWindows.Command!.Name);
        Assert.Null(onMac.Command);
        Assert.Equal($"no command for {Trigger.ForGesture(Up).Describe()}", onMac.Reason);
    }

    [Fact]
    public void AnAppCommandInACategoryNotUsedHereFallsThroughToGlobal_AsACommandNotUsedHereDoes()
    {
        var pcOnly = NewCategory("PC only") with { UseOn = PlatformSet.Windows };
        var chrome = NewGroup("Chrome", new AppMatcher { WindowsProcessNames = ["chrome.exe"], MacProcessNames = ["Google Chrome"] }, NewCommand("Close tab", Up).In(pcOnly)) with { Categories = [pcOnly] };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Close window", Up)), chrome));

        Assert.Equal("Close tab", CommandResolver.Resolve(mapping, Window("chrome.exe"), Trigger.ForGesture(Up), Windows).Command!.Name);
        var onMac = CommandResolver.Resolve(mapping, Window("Google Chrome"), Trigger.ForGesture(Up), Mac);
        Assert.Equal("Close window", onMac.Command!.Name);
        Assert.Equal("global", onMac.Reason);
    }

    [Fact]
    public void AWindowsOnlyCommandInAnAllPlatformCategoryFollowsItsOwnUseOn()
    {
        var window = NewCategory("Window");
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Show desktop", Up).In(window) with { UseOn = PlatformSet.Windows }) with { Categories = [window] }));

        Assert.Equal("Show desktop", CommandResolver.Resolve(mapping, Window(), Trigger.ForGesture(Up), Windows).Command!.Name);
        Assert.Null(CommandResolver.Resolve(mapping, Window("TextEdit"), Trigger.ForGesture(Up), Mac).Command);
    }

    [Theory]
    [InlineData(PlatformSet.All, PlatformSet.All, PlatformSet.All, PlatformSet.All, PlatformSet.All)]
    [InlineData(PlatformSet.All, PlatformSet.Windows, PlatformSet.All, PlatformSet.Windows, PlatformSet.Windows)]
    [InlineData(PlatformSet.All, PlatformSet.All, PlatformSet.MacOS, PlatformSet.All, PlatformSet.MacOS)]
    [InlineData(PlatformSet.MacOS, PlatformSet.All, PlatformSet.All, PlatformSet.MacOS, PlatformSet.MacOS)]
    [InlineData(PlatformSet.All, PlatformSet.Windows, PlatformSet.MacOS, PlatformSet.Windows, PlatformSet.None)]
    [InlineData(PlatformSet.Windows, PlatformSet.MacOS, PlatformSet.All, PlatformSet.None, PlatformSet.None)]
    public void ACommandIsUsedWhereItsGroupItsCategoryAndItselfAllInclude(PlatformSet group, PlatformSet category, PlatformSet command, PlatformSet limit, PlatformSet effective)
    {
        var section = NewCategory("Section") with { UseOn = category };
        var stored = NewCommand("Act", Up).In(section) with { UseOn = command };
        var app = NewGroup("App", commands: [stored]) with { UseOn = group, Categories = [section] };

        Assert.Equal(limit, app.UseOnLimitFor(stored));
        Assert.Equal(effective, app.EffectiveUseOn(stored));
        Assert.Equal(effective.Includes(Windows), app.IsCommandUsedOn(stored, Windows));
        Assert.Equal(effective.Includes(Mac), app.IsCommandUsedOn(stored, Mac));
    }

    [Fact]
    public void AnUncategorizedCommandAndOneNamingAMissingCategoryAreLimitedByNoCategory_AndGlobalByNoGroup()
    {
        var command = NewCommand("Act", Up) with { UseOn = PlatformSet.MacOS };
        var global = NewGlobal(command) with { UseOn = PlatformSet.Windows };

        Assert.Equal(PlatformSet.All, global.UseOnLimitFor(command));
        Assert.True(global.IsCommandUsedOn(command, Mac));
        Assert.Null(global.CategoryOf(command));
        var stray = command with { CategoryId = CategoryId.New() };
        Assert.Null(global.CategoryOf(stray));
        Assert.Equal(PlatformSet.MacOS, global.EffectiveUseOn(stray));
    }

    [Fact]
    public void ACategoryMustBeUsedSomewhere_TheCommandsOwnValueIsNeverChangedByIt()
    {
        var nowhere = NewCategory("Personal") with { UseOn = PlatformSet.None };
        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal() with { Categories = [nowhere] })));
        Assert.Equal("Use 'Personal' on at least one platform.", ex.Message);

        var personal = NewCategory("Personal") with { UseOn = PlatformSet.Windows };
        var store = new MappingStore(Document(NewGlobal(NewCommand("Act", Up).In(personal)) with { Categories = [personal] }));
        store.UpdateGroup(store.Global with { Categories = [personal with { UseOn = PlatformSet.All }] });

        Assert.Equal(PlatformSet.All, Assert.Single(store.Global.Commands).UseOn);
        Assert.True(store.Global.IsCommandUsedOn(store.Global.Commands[0], Mac));
        Assert.True(store.Undo());
        Assert.False(store.Global.IsCommandUsedOn(store.Global.Commands[0], Mac));
    }

    [Fact]
    public void TheFileWritesACategorysUseOnOnlyWhenSet_AndReadsItBack()
    {
        var media = NewCategory("Media");
        var personal = NewCategory("Personal") with { UseOn = PlatformSet.Windows };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Play", Up).In(media), NewCommand("Desk", Down).In(personal)) with { Categories = [media, personal] }));

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add).Mapping;

        Assert.Empty(_notices);
        Assert.Equal(1, Count(json, "\"useOn\""));
        Assert.Contains($"{{\"id\":\"{personal.Id}\",\"name\":\"Personal\",\"useOn\":[\"windows\"]}}", Compact(json), StringComparison.Ordinal);
        Assert.Equal(PlatformSet.Windows, back.Global.FindCategory(personal.Id)!.UseOn);
        Assert.Equal(PlatformSet.All, back.Global.FindCategory(media.Id)!.UseOn);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = back }));
    }

    [Fact]
    public void AFileWithoutUseOnOnItsCategoriesReadsThemAsEverywhere()
    {
        var back = Read("""[ { "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Media" } ]""");

        Assert.Empty(_notices);
        Assert.Equal(PlatformSet.All, Assert.Single(back.Global.Categories).UseOn);
    }

    [Fact]
    public void ACategoryUseOnThatIsNotAListOfNamesNeverFailsALoad_ItIsEverywhereWithANotice()
    {
        var back = Read("""[ { "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Media", "useOn": "windows" } ]""");

        Assert.Equal(PlatformSet.All, Assert.Single(back.Global.Categories).UseOn);
        Assert.Equal(["Category 'Media' of app group 'Global': 'useOn' must be a list of platform names; the category is used on every platform."], _notices);
    }

    private MappingDocument Read(string categories)
        => ConfigSerializer.Read(
            $$"""
            { "schemaVersion": 1, "mapping": { "groups": [ { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", "categories": {{categories}}, "commands": [] } ] } }
            """,
            FakeStepType.Registry,
            _notices.Add).Mapping;

    private static string Compact(string json) => string.Concat(json.Where(c => !char.IsWhiteSpace(c)));

    private static int Count(string text, string part) => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;
}
