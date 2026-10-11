using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// What a command's step editor may add (<see cref="StepOffer"/>; Joel, 0.11.3: the magnifier's only step is its Remap step, yet
/// "New step…" offered everything): Remap only under a hold remap or on a button trigger, and the step rules validation runs, with
/// the same reason validation gives. A step changed to another type (Joel, 2026-10-11) is judged in that step's place.
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

    /// <summary>"Change to…" (Joel, 2026-10-11) judges a replacement, not an addition: the only Remap step may become an ordinary step.</summary>
    [Fact]
    public void ChangingARemapStepThatIsTheOnlyStep_ToAnOrdinaryType_IsAllowed_ItsOwnTypeIsRefused()
    {
        var space = Blender.NewSpace();
        var orbit = Blender.Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Blender.Middle);

        Assert.Null(StepOffer.CheckChange(HotkeyStepType.Instance, orbit, 0, Windows));
        Assert.Equal("This step is Remap already.", StepOffer.CheckChange(RemapStepType.Instance, orbit, 0, Windows));
        var refusal = Assert.Single(StepOffer.ChangeRefusals(Types, orbit, 0, Windows));
        Assert.Same(RemapStepType.Instance, refusal.Key);
        Assert.Equal(StepOffer.Already(RemapStepType.Instance), refusal.Value);

        // Adding is another question: the same command takes no step more.
        Assert.Equal(OnlyStep, StepOffer.Check(HotkeyStepType.Instance, orbit, Windows));
    }

    [Fact]
    public void TheStepsOwnTypeIsRefused_InItsTypesWords()
    {
        var three = NewCommand("Three", Up, new CommandStep(new DelayStep(10), Windows), new CommandStep(HotkeyStepType.Instance.CreateDefault(), Windows));

        Assert.Equal("This step is Delay already.", StepOffer.CheckChange(DelayStepType.Instance, three, 0, Windows));
        Assert.Equal("This step is Hotkey already.", StepOffer.CheckChange(HotkeyStepType.Instance, three, 1, Windows));
        Assert.Null(StepOffer.CheckChange(HotkeyStepType.Instance, three, 0, Windows));
        Assert.Equal("This step is Hotkey already.", StepOffer.Already(HotkeyStepType.Instance));
    }

    [Fact]
    public void AStepAmongOthers_CannotBecomeARemapStep_TheOnlyStepOfAHoldRemapsCommandCan()
    {
        var space = Blender.NewSpace();
        var note = Blender.Steps(space, "Note", new HoldInput.Key(KeyCode.Q));
        var two = note with { Steps = [.. note.Steps, new CommandStep(new DelayStep(10), Windows)] };
        var chord = NewCommand("Magnifier", RightLeft, new CommandStep(new DelayStep(10), Windows), new CommandStep(new DelayStep(20), Windows));
        var plain = NewCommand("Close window", Up, NewStep("close"));

        Assert.Equal(OnlyStep, StepOffer.CheckChange(RemapStepType.Instance, two, 1, Windows));
        Assert.Equal(OnlyStep, StepOffer.CheckChange(RemapStepType.Instance, chord, 0, Windows));
        Assert.Equal(NotHere, StepOffer.CheckChange(RemapStepType.Instance, plain, 0, Windows));

        // The only step of a command under a hold remap may become a Remap step: "New step…"'s, fitted to the input.
        Assert.Null(StepOffer.CheckChange(RemapStepType.Instance, note, 0, Windows));
        Assert.Empty(StepOffer.ChangeRefusals(Types, note, 0, Windows));
        var nudge = Blender.Steps(space, "Nudge", new HoldInput.Wheel(WheelDirection.Down));
        Assert.Null(StepOffer.CheckChange(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Down)), nudge, 0, Windows));
        Assert.Equal("On a wheel input, a Remap step's output is a key or a wheel notch, not a button.", StepOffer.CheckChange(new RemapStep(Blender.Middle), nudge, 0, Windows));
    }

    [Fact]
    public void AButtonTriggersRemapStep_MayBecomeAHotkeyStep_AndAnOnlyStepMayBecomeARemapStep()
    {
        var magnifier = NewCommand("Magnifier", RightLeft, new CommandStep(WinShiftX, Windows));
        var delayed = NewCommand("Magnifier", RightLeft, new CommandStep(new DelayStep(10), Windows));

        Assert.Null(StepOffer.CheckChange(HotkeyStepType.Instance, magnifier, 0, Windows));
        Assert.Equal(StepOffer.Already(RemapStepType.Instance), Assert.Single(StepOffer.ChangeRefusals(Types, magnifier, 0, Windows)).Value);
        Assert.Null(StepOffer.CheckChange(RemapStepType.Instance, delayed, 0, Windows));
    }

    [Fact]
    public void TheDraftedTrigger_DecidesWhetherAStepMayBecomeARemapStep()
    {
        var stored = NewCommand("Magnifier", Up, new CommandStep(new DelayStep(10), Windows));
        var chord = NewCommand("Magnifier", RightLeft, new CommandStep(new DelayStep(10), Windows));

        Assert.Equal(NotHere, StepOffer.CheckChange(RemapStepType.Instance, stored, 0, Windows));
        Assert.Null(StepOffer.CheckChange(RemapStepType.Instance, stored, 0, Windows, drafted: RightLeft));
        Assert.Equal(NotHere, StepOffer.CheckChange(RemapStepType.Instance, chord, 0, Windows, drafted: Trigger.ForGesture(Up)));
    }

    [Fact]
    public void AChangeOfAStepTheCommandDoesNotHave_Throws()
    {
        var close = NewCommand("Close window", Up, NewStep("close"));

        Assert.Throws<ArgumentOutOfRangeException>(() => StepOffer.CheckChange(DelayStepType.Instance, close, 1, Windows));
        Assert.Throws<ArgumentOutOfRangeException>(() => StepOffer.CheckChange(DelayStepType.Instance, close, -1, Windows));
    }

    /// <summary>A changed step: the offer refuses exactly the replacements the store's validation refuses, each with its rule's sentence.</summary>
    [Fact]
    public void AChangedStep_IsRefusedExactlyWhenValidationRefusesTheCommandWithIt()
    {
        var space = Blender.NewSpace();
        var orbit = Blender.Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Blender.Middle);
        var two = Blender.Steps(space, "Note", new HoldInput.Key(KeyCode.Q)) with { Steps = [NewStep("a"), NewStep("b")] };
        var note = Blender.Steps(space, "Note", new HoldInput.Key(KeyCode.Q));
        var cases = new (Command Command, int Index, IStep Changed, string? Reason)[]
        {
            (orbit, 0, new DelayStep(10), null),
            (note, 0, new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up)), "A wheel notch output is for a wheel input only."),
            (note, 0, new RemapStep(Blender.G), null),
            (two, 1, new RemapStep(Blender.Middle), OnlyStep),
            (two, 1, new DelayStep(10), null),
            (NewCommand("Magnifier", RightLeft, new CommandStep(new DelayStep(10), Windows)), 0, new RemapStep(new RemapOutput.Button(MouseButton.Middle)), "On a button trigger, a Remap step's output is a key."),
            (NewCommand("Magnifier", RightLeft, new CommandStep(new DelayStep(10), Windows)), 0, WinShiftX, null),
        };

        foreach (var (command, index, changed, reason) in cases)
        {
            var steps = command.Steps.ToList();
            steps[index] = steps[index] with { Step = changed };
            var withIt = command with { Steps = steps };
            var document = command.HoldRemapId is null
                ? Document(NewGlobal(withIt))
                : Document(NewGlobal(), NewGroup("Blender", null, withIt) with { HoldRemaps = [space] });

            Assert.Equal(reason, StepOffer.CheckChange(changed, command, index, Windows));
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
