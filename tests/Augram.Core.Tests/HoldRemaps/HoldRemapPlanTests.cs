using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Xunit;
using static Augram.Core.Tests.HoldRemaps.Support.Blender;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>What the hook reads (<see cref="HoldRemapPlan"/>): built for one app group on one platform, inputs as bit sets, commands resolved for that platform.</summary>
public sealed class HoldRemapPlanTests
{
    [Fact]
    public void BlendersPlanHasSpaceWithItsInputsAsBitSets()
    {
        var plan = HoldRemapPlan.ForGroup(Document().Groups[1], HostPlatform.Windows);

        var space = Assert.Single(plan.Entries);
        Assert.Same(space, plan.Find(KeyCode.Space));
        Assert.Null(plan.Find(KeyCode.W));
        Assert.Equal(("Blender", 180), (plan.GroupName, space.TapTimeMs));
        Assert.Equal(HeldButtons.Left | HeldButtons.Right | HeldButtons.Middle, space.Buttons);
        Assert.False(space.IsInput(MouseButton.X1));
        Assert.True(space.IsInput(WheelDirection.Down));
        Assert.False(space.IsInput(WheelDirection.Up));
        Assert.All(new[] { KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.F, KeyCode.Q }, key => Assert.True(space.IsInput(key)));
        Assert.False(space.IsInput(KeyCode.A));
        Assert.False(space.IsInput(KeyCode.Space));
        Assert.Equal("Zoom both", space.ForButtons(HeldButtons.Left | HeldButtons.Right)?.Name);
        Assert.Null(space.ForButtons(HeldButtons.Left | HeldButtons.Middle));
    }

    [Fact]
    public void HoldsButtonOutput_IsTrueExactlyForTheSetsWhoseCommandHoldsAButton()
    {
        var space = NewSpace();
        var group = Group(space) with
        {
            Commands = [.. Group(space).Commands, Remap(space, "Key", HoldInput.Of(MouseButton.X1), G), Support.Blender.Steps(space, "Run", HoldInput.Of(MouseButton.X2))],
        };

        var entry = HoldRemapPlan.ForGroup(Document(group).Groups[1], HostPlatform.Windows).Entries.Single();

        Assert.All(
            new[] { HeldButtons.Left, HeldButtons.Right, HeldButtons.Middle, HeldButtons.Left | HeldButtons.Right },
            set => Assert.True(entry.HoldsButtonOutput(set), set.ToString()));
        Assert.False(entry.HoldsButtonOutput(HeldButtons.None));
        Assert.False(entry.HoldsButtonOutput(HeldButtons.Left | HeldButtons.Middle), "no command for the set");
        Assert.False(entry.HoldsButtonOutput(HeldButtons.X1), "a key output");
        Assert.False(entry.HoldsButtonOutput(HeldButtons.X2), "a Steps command");
        Assert.False(entry.HoldsButtonOutput(HeldButtons.Stroke | HeldButtons.Left), "not a set of physical buttons");
    }

    [Fact]
    public void BindingsSayWhatEachCommandDoes()
    {
        var space = NewSpace();
        var silent = new Command(CommandId.New(), "Silent", Trigger.ForInput(new HoldInput.Key(KeyCode.Z)), IsActive: true, []) { HoldRemapId = space.Id };
        var off = Remap(space, "Off", new HoldInput.Key(KeyCode.X), G);
        off = off with { Steps = [off.Steps[0] with { IsActive = false }] };
        var unset = Remap(space, "Unset", new HoldInput.Key(KeyCode.C), new RemapOutput.Key(KeyCode.None));
        var group = Group(space) with { Commands = [.. Group(space).Commands, silent, off, unset] };

        var entry = HoldRemapPlan.ForGroup(Document(group).Groups[1], HostPlatform.Windows).Entries.Single();

        Assert.Equal(Middle, entry.ForButtons(HeldButtons.Left)!.Output);
        Assert.True(entry.ForKey(KeyCode.Q)!.RunsSteps);
        Assert.Null(entry.ForKey(KeyCode.Q)!.Output);
        Assert.All(new[] { KeyCode.Z, KeyCode.X, KeyCode.C }, key =>
        {
            var binding = entry.ForKey(key)!;
            Assert.Null(binding.Output);
            Assert.False(binding.RunsSteps);
        });
    }

    [Fact]
    public void APlatformVersionsOutputIsUsedWhereItHasOne()
    {
        var space = NewSpace();
        var zoom = Remap(space, "Zoom", HoldInput.Of(MouseButton.Middle), CtrlMiddle);
        zoom = zoom.WithStepsFor(HostPlatform.MacOS, [new CommandStep(new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Meta)), HostPlatform.MacOS)], DateTimeOffset.UnixEpoch);
        var document = Document(NewGroup("Blender", ByProcess("blender.exe"), zoom) with { HoldRemaps = [space] });

        var windows = HoldRemapPlan.ForGroup(document.Groups[1], HostPlatform.Windows).Entries.Single().ForButtons(HeldButtons.Middle)!;
        var mac = HoldRemapPlan.ForGroup(document.Groups[1], HostPlatform.MacOS).Entries.Single().ForButtons(HeldButtons.Middle)!;

        Assert.Equal(CtrlMiddle, windows.Output);
        Assert.Equal(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Meta), mac.Output);
    }

    [Fact]
    public void OnlyWhatIsActiveAndUsedHereCounts()
    {
        var space = NewSpace();
        var group = Group(space);
        var commands = group.Commands.Select(command => command.Name switch
        {
            "Orbit" => command with { IsActive = false },
            "Grab" => command with { UseOn = PlatformSet.MacOS },
            _ => command,
        });
        var document = Document(group with { Commands = [.. commands] });

        var windows = HoldRemapPlan.ForGroup(document.Groups[1], HostPlatform.Windows).Entries.Single();
        var mac = HoldRemapPlan.ForGroup(document.Groups[1], HostPlatform.MacOS).Entries.Single();

        Assert.Null(windows.ForButtons(HeldButtons.Left));
        Assert.True(windows.IsInput(MouseButton.Left), "Left is still in Left + Right");
        Assert.False(windows.IsInput(KeyCode.W));
        Assert.True(mac.IsInput(KeyCode.W));
    }

    [Fact]
    public void NoEntryForAHoldRemapInactiveNotUsedHereOrWithoutAKey_NoPlanForAGroupInactiveOrNotUsedHere()
    {
        var space = NewSpace();
        Assert.True(Plan(Group(space with { IsActive = false })).IsEmpty);
        Assert.True(Plan(Group(space with { UseOn = PlatformSet.MacOS })).IsEmpty);
        Assert.True(Plan(Group(space with { HoldKey = KeyCode.None })).IsEmpty);
        Assert.True(Plan(Group(space) with { IsActive = false }).IsEmpty);
        Assert.True(Plan(Group(space) with { UseOn = PlatformSet.MacOS }).IsEmpty);
        Assert.Same(HoldRemapPlan.Empty, HoldRemapPlan.ForGroup(null, HostPlatform.Windows));
        Assert.Same(HoldRemapPlan.Empty, HoldRemapPlan.ForGroup(AppGroup.EmptyGlobal, HostPlatform.Windows));
    }

    [Fact]
    public void ThePlanForTheForegroundWindowIsItsAppGroups_OnTheMacThroughTheKnownAppGuess()
    {
        var document = Document();

        Assert.Equal("Blender", HoldRemapPlan.For(document, Window("blender.exe"), HostPlatform.Windows).GroupName);
        Assert.Equal("Blender", HoldRemapPlan.For(document, Window("Blender"), HostPlatform.MacOS).GroupName);
        Assert.True(HoldRemapPlan.For(document, Window("chrome.exe"), HostPlatform.Windows).IsEmpty);
        Assert.True(HoldRemapPlan.For(document, null, HostPlatform.Windows).IsEmpty);
    }

    [Fact]
    public void TheFocusIsWorthWatching_OnlyWhileSomeActiveHoldRemapCanMatchHere()
    {
        var space = NewSpace();
        var blender = Group(space);

        Assert.True(HoldRemapPlan.WatchesFocus(Document(blender), HostPlatform.Windows));
        Assert.True(HoldRemapPlan.WatchesFocus(Document(blender), HostPlatform.MacOS), "Blender.exe has a known Mac name");
        Assert.False(HoldRemapPlan.WatchesFocus(Document(blender with { IsActive = false }), HostPlatform.Windows));
        Assert.False(HoldRemapPlan.WatchesFocus(Document(blender with { HoldRemaps = [space with { IsActive = false }] }), HostPlatform.Windows));
        Assert.False(HoldRemapPlan.WatchesFocus(Document(blender with { HoldRemaps = [space with { UseOn = PlatformSet.Windows }] }), HostPlatform.MacOS));
        Assert.False(HoldRemapPlan.WatchesFocus(Document(blender with { Matcher = ByProcess("nothing-known.exe") }), HostPlatform.MacOS), "no name to match on a Mac");
        Assert.False(HoldRemapPlan.WatchesFocus(Document(NewGroup("Chrome", ByProcess("chrome.exe"))), HostPlatform.Windows));
    }

    [Theory]
    [InlineData(KeyCode.None)]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.Space)]
    [InlineData(KeyCode.F24)]
    [InlineData(KeyCode.NumPadEnter)]
    [InlineData(KeyCode.AppCalculator)]
    public void AKeySetHoldsExactlyTheKeysPutInIt(KeyCode key)
    {
        var set = KeySet.Empty.With(key);

        Assert.True(set.Contains(key));
        Assert.All(Enum.GetValues<KeyCode>().Where(other => other != key), other => Assert.False(set.Contains(other)));
        Assert.False(set.Contains((KeyCode)256));
        Assert.Equal(set, set.With((KeyCode)(-1)));
    }

    private static HoldRemapPlan Plan(AppGroup group) => HoldRemapPlan.ForGroup(Document(group).Groups[1], HostPlatform.Windows);
}
