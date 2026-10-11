using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Xunit;
using static Augram.Core.Tests.HoldRemaps.Support.Blender;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// The Remap step as <see cref="HoldRemapRules"/> names it for everyone else (plan 0007, the documented exception): a step fitted
/// to a trigger, and the output a command plays.
/// </summary>
public sealed class RemapStepRulesTests
{
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));

    [Fact]
    public void AStepIsFittedToAnInput_OrToAButtonTrigger_AndLeftAloneOtherwise()
    {
        var ctrlMiddle = new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control));
        var other = NewStep("other").Step;

        Assert.Equal(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Control)), HoldRemapRules.FittedTo(ctrlMiddle, Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up))));
        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None, KeyModifiers.Control)), HoldRemapRules.FittedTo(ctrlMiddle, RightLeft));
        Assert.Equal(new RemapStep(G), HoldRemapRules.FittedTo(new RemapStep(G), RightLeft));
        Assert.Same(ctrlMiddle, HoldRemapRules.FittedTo(ctrlMiddle, Trigger.ForGesture(Up)));
        Assert.Same(other, HoldRemapRules.FittedTo(other, RightLeft));
    }

    [Fact]
    public void ACommandPlaysItsOneActiveRemapStepsOutput_WhenItIsSet()
    {
        var space = NewSpace();
        var orbit = Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle);
        var unset = Remap(space, "Unset", HoldInput.Of(MouseButton.Right), new RemapOutput.Key(KeyCode.None));
        var off = orbit with { Steps = [orbit.Steps[0] with { IsActive = false }] };
        var two = orbit with { Steps = [.. orbit.Steps, NewStep("more")] };

        Assert.Equal(Middle, orbit.RemapOutputOn(HostPlatform.Windows));
        Assert.Equal(Middle, orbit.RemapOutputOn(HostPlatform.MacOS));
        Assert.Null(unset.RemapOutputOn(HostPlatform.Windows));
        Assert.Null(off.RemapOutputOn(HostPlatform.Windows));
        Assert.Null(two.RemapOutputOn(HostPlatform.Windows));
        Assert.Null(Support.Blender.Steps(space, "Note", new HoldInput.Key(KeyCode.Q)).RemapOutputOn(HostPlatform.Windows));
    }
}
