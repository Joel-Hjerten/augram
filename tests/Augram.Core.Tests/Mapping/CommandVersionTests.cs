using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Xunit;

namespace Augram.Core.Tests.Mapping;

/// <summary>F8 own versions (Joel, 2026-10-07): one command, the original's steps where it was authored, its own steps on the other platform.</summary>
public sealed class CommandVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 22, 0, 0, TimeSpan.Zero);
    private static readonly CommandStep CtrlBackspace = new(new HotkeyStep(KeyModifiers.Control, KeyCode.Backspace), HostPlatform.Windows);
    private static readonly CommandStep WinD = new(new HotkeyStep(KeyModifiers.Meta, KeyCode.D), HostPlatform.Windows);
    private static readonly CommandStep OptBackspace = new(new HotkeyStep(KeyModifiers.Alt, KeyCode.Backspace), HostPlatform.MacOS);

    [Fact]
    public void WithoutAnOwnVersionTheOtherPlatformRunsTheConvertedOriginal()
    {
        var command = Make(CtrlBackspace, WinD);

        Assert.Equal(HostPlatform.Windows, command.Origin);
        var onMac = command.PlanFor(HostPlatform.MacOS);
        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.Backspace), onMac[0].Run.Step);
        Assert.Equal(StepConversionKind.NotConvertible, onMac[1].Run.Kind);
        Assert.Same(CtrlBackspace.Step, command.PlanFor(HostPlatform.Windows)[0].Run.Step);
    }

    [Fact]
    public void EditingOnTheOtherPlatformMakesItsOwnSteps_TheOriginalKeepsRunningWhereItWasAuthored()
    {
        var command = Make(CtrlBackspace);

        var edited = command.WithStepsFor(HostPlatform.MacOS, [OptBackspace], Now);

        Assert.Equal([CtrlBackspace], edited.Steps);
        Assert.Equal(HostPlatform.MacOS, edited.OwnVersion!.Platform);
        Assert.Equal([OptBackspace], edited.StepsFor(HostPlatform.MacOS));
        Assert.Same(OptBackspace.Step, edited.PlanFor(HostPlatform.MacOS)[0].Run.Step);
        Assert.Same(CtrlBackspace.Step, edited.PlanFor(HostPlatform.Windows)[0].Run.Step);
        Assert.Equal(Now, edited.OwnVersion.ChangedAt);
        Assert.False(edited.IsOwnVersionStale);
        Assert.Equal(HostPlatform.Windows, edited.Origin);
    }

    [Fact]
    public void EditingTheOriginalFlagsTheOwnVersion_UntilItIsCheckedOrEdited()
    {
        var withMac = Make(CtrlBackspace).WithStepsFor(HostPlatform.MacOS, [OptBackspace], Now);

        var changed = withMac.WithStepsFor(HostPlatform.Windows, [CtrlBackspace, WinD], Now);
        Assert.Equal([CtrlBackspace, WinD], changed.Steps);
        Assert.True(changed.IsOwnVersionStale);

        Assert.False(changed.WithOwnVersionChecked(Now).IsOwnVersionStale);
        Assert.False(changed.WithStepsFor(HostPlatform.MacOS, [OptBackspace, OptBackspace], Now).IsOwnVersionStale);
    }

    [Fact]
    public void UsingTheConvertedOriginalAgainDropsTheOwnVersion()
    {
        var withMac = Make(CtrlBackspace).WithStepsFor(HostPlatform.MacOS, [OptBackspace], Now);

        var back = withMac.WithoutOwnVersion();

        Assert.Null(back.OwnVersion);
        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.Backspace), back.PlanFor(HostPlatform.MacOS)[0].Run.Step);
    }

    [Fact]
    public void TheConvertedStartAuthorsGuessesHere_AndKeepsAStepWithNoGuessAsItIs()
    {
        var converted = Make(CtrlBackspace, WinD, new CommandStep(new DelayStep(30), HostPlatform.Windows, IsActive: false)).ConvertedFor(HostPlatform.MacOS);

        Assert.Equal(new CommandStep(new HotkeyStep(KeyModifiers.Meta, KeyCode.Backspace), HostPlatform.MacOS), converted[0]);
        Assert.Same(WinD, converted[1]);
        Assert.Equal(new CommandStep(new DelayStep(30), HostPlatform.MacOS, IsActive: false), converted[2]);
    }

    [Fact]
    public void ACommandWithNoStepsTakesTheEditingPlatformAsItsOrigin_AndAnEmptyOwnVersionDoesNothingThere()
    {
        var empty = Make();
        Assert.Null(empty.Origin);
        var macFirst = empty.WithStepsFor(HostPlatform.MacOS, [OptBackspace], Now);
        Assert.Null(macFirst.OwnVersion);
        Assert.Equal(HostPlatform.MacOS, macFirst.Origin);

        var nothingOnMac = Make(CtrlBackspace).WithStepsFor(HostPlatform.MacOS, [], Now);
        Assert.True(nothingOnMac.IsOverrideToNothingOn(HostPlatform.MacOS));
        Assert.False(nothingOnMac.IsOverrideToNothingOn(HostPlatform.Windows));
    }

    [Fact]
    public void TheFingerprintFollowsTheStepsOnly()
    {
        Assert.Equal(Command.Fingerprint([CtrlBackspace, WinD]), Command.Fingerprint([CtrlBackspace with { }, WinD with { }]));
        Assert.NotEqual(Command.Fingerprint([CtrlBackspace]), Command.Fingerprint([CtrlBackspace with { IsActive = false }]));
        Assert.NotEqual(Command.Fingerprint([CtrlBackspace]), Command.Fingerprint([WinD]));
        Assert.Equal(16, Command.Fingerprint([]).Length);
    }

    private static Command Make(params CommandStep[] steps) => new(CommandId.New(), "Delete word", Trigger.None, IsActive: true, steps);
}
