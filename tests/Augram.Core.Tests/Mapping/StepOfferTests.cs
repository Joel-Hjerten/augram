using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// What a command's step editor may add (<see cref="StepOffer"/>; Joel, 0.11.3: the magnifier's only step is its Remap step, yet
/// "New step…" offered everything): Remap only under a hold remap or on a button trigger, and the step rules validation runs, with
/// the same reason validation gives.
/// </summary>
public sealed class StepOfferTests
{
    private const string OnlyStep = "A Remap step is a command's only step.";
    private const string NotHere = "Remap is for a command under a hold remap or with a button trigger.";
    private const HostPlatform Windows = HostPlatform.Windows;

    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));
    private static readonly RemapStep WinShiftX = new(new RemapOutput.Key(KeyCode.X, KeyModifiers.Meta | KeyModifiers.Shift));

    private static IReadOnlyList<IStepType> Types => StepRegistry.BuiltIn.All;

    [Fact]
    public void APlainGestureCommand_TakesOrdinarySteps_ButNotRemap()
    {
        var close = NewCommand("Close window", Up, NewStep("close"));

        Assert.Null(StepOffer.Check(DelayStepType.Instance, close, Windows));
        Assert.Equal(NotHere, StepOffer.Check(RemapStepType.Instance, close, Windows));
        Assert.Equal(NotHere, StepOffer.Check(WinShiftX, close, Windows));
        var refusal = Assert.Single(StepOffer.Refusals(Types, close, Windows));
        Assert.Same(RemapStepType.Instance, refusal.Key);
        Assert.Equal(NotHere, refusal.Value);
        Assert.Equal(NotHere, StepOffer.NotHere(RemapStepType.Instance));
    }

    [Fact]
    public void UnderAHoldRemapWithNoSteps_EveryTypeIsOffered_AndANewRemapStepFitsTheInput()
    {
        var space = Blender.NewSpace();
        var zoom = new Command(CommandId.New(), "Zoom in", Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up)), IsActive: true, []) { HoldRemapId = space.Id };
        var noInput = zoom with { Trigger = Trigger.None };

        Assert.Empty(StepOffer.Refusals(Types, zoom, Windows));
        Assert.Empty(StepOffer.Refusals(Types, noInput, Windows));
        Assert.Equal(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up)), StepOffer.NewStep(RemapStepType.Instance, zoom, Windows));
        Assert.Equal(RemapStepType.Instance.CreateDefault(), StepOffer.NewStep(RemapStepType.Instance, noInput, Windows));
        Assert.Equal(DelayStepType.Instance.CreateDefault(), StepOffer.NewStep(DelayStepType.Instance, zoom, Windows));
    }

    [Fact]
    public void UnderAHoldRemapWithARemapStep_EveryTypeIsRefused_ForTheOnlyStepRule()
    {
        var space = Blender.NewSpace();
        var orbit = Blender.Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Blender.Middle);

        var refusals = StepOffer.Refusals(Types, orbit, Windows);

        Assert.Equal(Types.Count, refusals.Count);
        Assert.All(refusals.Values, reason => Assert.Equal(OnlyStep, reason));
        Assert.Equal(OnlyStep, StepOffer.Check(new DelayStep(10), orbit, Windows));
    }

    [Fact]
    public void UnderAHoldRemapWithAnOrdinaryStep_RemapIsRefused_OrdinaryStepsAreNot()
    {
        var space = Blender.NewSpace();
        var note = Blender.Steps(space, "Note", new HoldInput.Key(KeyCode.Q));

        var refusal = Assert.Single(StepOffer.Refusals(Types, note, Windows));

        Assert.Same(RemapStepType.Instance, refusal.Key);
        Assert.Equal(OnlyStep, refusal.Value);
        Assert.Null(StepOffer.Check(new DelayStep(10), note, Windows));
    }

    [Fact]
    public void AButtonTriggerWithNoSteps_TakesRemapWithAKeyOutput_AndOrdinarySteps_WithItsRemapStepNothingMore()
    {
        var magnifier = NewCommand("Magnifier", RightLeft);

        Assert.Empty(StepOffer.Refusals(Types, magnifier, Windows));
        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None)), StepOffer.NewStep(RemapStepType.Instance, magnifier, Windows));

        // Joel's magnifier (0.11.3): its one Remap step holds Win+Shift+X, and it takes no other step.
        var joels = magnifier with { Steps = [new CommandStep(WinShiftX, Windows)] };
        var refusals = StepOffer.Refusals(Types, joels, Windows);
        Assert.Equal(Types.Count, refusals.Count);
        Assert.All(refusals.Values, reason => Assert.Equal(OnlyStep, reason));
    }

    [Fact]
    public void TheDraftedTrigger_DecidesWhereRemapMayAppear_AndItsOutput()
    {
        var stored = NewCommand("Magnifier", Up);
        var chord = NewCommand("Magnifier", RightLeft);

        Assert.Null(StepOffer.Check(RemapStepType.Instance, stored, Windows, drafted: RightLeft));
        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None)), StepOffer.NewStep(RemapStepType.Instance, stored, Windows, drafted: RightLeft));
        Assert.Equal(NotHere, StepOffer.Check(RemapStepType.Instance, chord, Windows, drafted: Trigger.ForGesture(Up)));
    }

    [Fact]
    public void ValidationAndTheOffer_GiveTheSameReason_ForTheOnlyStepRule()
    {
        var space = Blender.NewSpace();
        var orbit = Blender.Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Blender.Middle);
        var both = orbit with { Steps = [.. orbit.Steps, new CommandStep(new DelayStep(10), Windows)] };

        var reason = StepOffer.Check(DelayStepType.Instance, orbit, Windows);
        var refusal = Refusal(Document(NewGlobal(), NewGroup("Blender", null, both) with { HoldRemaps = [space] }));

        Assert.Equal(OnlyStep, reason);
        Assert.Equal(OnlyStep, HoldRemapRules.StepsProblem(both));
        Assert.Equal("'Orbit' has a Remap step among other steps: a Remap step is a command's only step.", refusal);
        Assert.EndsWith($": {char.ToLowerInvariant(reason![0])}{reason[1..]}", refusal);
    }

    /// <summary>A pasted step: the offer refuses exactly the steps the store's validation refuses, each with its rule's sentence.</summary>
    [Fact]
    public void APastedStep_IsRefusedExactlyWhenValidationRefusesIt()
    {
        var space = Blender.NewSpace();
        var cases = new (Command Command, IStep Pasted, string? Reason)[]
        {
            (NewCommand("Magnifier", RightLeft), new RemapStep(new RemapOutput.Button(MouseButton.Middle)), "On a button trigger, a Remap step's output is a key."),
            (NewCommand("Magnifier", RightLeft), WinShiftX, null),
            (Empty(space, new HoldInput.Wheel(WheelDirection.Up)), new RemapStep(Blender.Middle), "On a wheel input, a Remap step's output is a key or a wheel notch, not a button."),
            (Empty(space, HoldInput.Of(MouseButton.Left)), new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up)), "A wheel notch output is for a wheel input only."),
            (Empty(space, HoldInput.Of(MouseButton.Left)), new RemapStep(Blender.Middle), null),
            (Blender.Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Blender.Middle), new RemapStep(Blender.Middle), OnlyStep),
        };

        foreach (var (command, pasted, reason) in cases)
        {
            var withIt = command with { Steps = [.. command.Steps, new CommandStep(pasted, Windows)] };
            var document = command.HoldRemapId is null
                ? Document(NewGlobal(withIt))
                : Document(NewGlobal(), NewGroup("Blender", null, withIt) with { HoldRemaps = [space] });

            Assert.Equal(reason, StepOffer.Check(pasted, command, Windows));
            Assert.Equal(reason is null, Refusal(document) is null);
        }
    }

    private static Command Empty(HoldRemap holdRemap, HoldInput input)
        => new(CommandId.New(), "Input", Trigger.ForInput(input), IsActive: true, []) { HoldRemapId = holdRemap.Id };

    private static string? Refusal(MappingDocument document)
    {
        try
        {
            MappingRules.ValidDocument(document);
            return null;
        }
        catch (MappingValidationException refusal)
        {
            return refusal.Message;
        }
    }
}
