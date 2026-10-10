using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps;
using Augram.Core.Steps.Scroll;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>
/// The Scroll executor over the recording fake: keys down in order, the wheel at the gesture start, keys up in reverse,
/// and every key that went down comes up again whatever failed (A19). Nothing here touches a real mouse or keyboard.
/// </summary>
public sealed class ScrollExecutionTests
{
    private static readonly ScrollStepType Type = ScrollStepType.Instance;

    [Fact]
    public void APlainScrollTurnsTheWheelAtTheGestureStart()
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new ScrollStep(ScrollDirection.Right, 2), StepContexts.Create(input: input));

        Assert.True(result.Succeeded);
        Assert.Equal(["scroll Right x2@100,200"], input.Calls);
    }

    [Fact]
    public void HeldKeysGoDownInOrderAndComeUpInReverse()
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new ScrollStep(ScrollDirection.Down, 3, KeyModifiers.Shift | KeyModifiers.Control), StepContexts.Create(input: input));

        Assert.True(result.Succeeded);
        string[] expected = ["press LeftControl", "press LeftShift", "scroll Down x3@100,200", "release LeftShift", "release LeftControl"];
        Assert.Equal(expected, input.Calls);
    }

    [Fact]
    public void ANotchCountOutOfRangeRunsClamped()
    {
        var input = new FakeInputSimulator();

        Type.Execute(new ScrollStep(ScrollDirection.Up, 0), StepContexts.Create(input: input));

        Assert.Equal(["scroll Up x1@100,200"], input.Calls);
    }

    [Fact]
    public void AFailedPressIsFailedWithNoScroll()
    {
        var input = new FakeInputSimulator { PressResult = SimulationResult.Unsupported };

        var result = Type.Execute(new ScrollStep(ScrollDirection.Up, 1, KeyModifiers.Control | KeyModifiers.Alt), StepContexts.Create(input: input));

        Assert.Equal((StepOutcome.Failed, "LeftControl press: Unsupported"), (result.Outcome, result.Reason));
        Assert.Equal(["press LeftControl"], input.Calls);
    }

    [Fact]
    public void AFailedScrollIsFailed_AndTheKeysStillComeUp()
    {
        var input = new FakeInputSimulator { OtherResult = SimulationResult.Failed };

        var result = Type.Execute(new ScrollStep(ScrollDirection.Up, 1, KeyModifiers.Control), StepContexts.Create(input: input));

        Assert.Equal((StepOutcome.Failed, "Ctrl + scroll up: Failed"), (result.Outcome, result.Reason));
        Assert.Equal(["press LeftControl", "scroll Up x1@100,200", "release LeftControl"], input.Calls);
    }

    [Fact]
    public void AFailedReleaseIsFailedAfterEveryKeyWasReleased()
    {
        var input = new FakeInputSimulator { ReleaseResult = SimulationResult.Failed };

        var result = Type.Execute(new ScrollStep(ScrollDirection.Down, 1, KeyModifiers.Control | KeyModifiers.Meta), StepContexts.Create(input: input));

        Assert.Equal((StepOutcome.Failed, "LeftMeta release: Failed"), (result.Outcome, result.Reason));
        Assert.Equal(["press LeftControl", "press LeftMeta", "scroll Down x1@100,200", "release LeftMeta", "release LeftControl"], input.Calls);
    }

    [Fact]
    public void ASimulatorThatThrowsStillGetsEveryPressedKeyReleased()
    {
        var input = new ThrowingScroll();

        Assert.Throws<InvalidOperationException>(() => Type.Execute(new ScrollStep(ScrollDirection.Up, 1, KeyModifiers.Control | KeyModifiers.Shift), StepContexts.Create(input: input)));

        Assert.Equal(["press LeftControl", "press LeftShift", "release LeftShift", "release LeftControl"], input.Keys);
    }

    /// <summary>Records keys and throws from the wheel, as a broken input library would.</summary>
    private sealed class ThrowingScroll : IInputSimulator
    {
        public List<string> Keys { get; } = [];

        public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y) => throw new InvalidOperationException("wheel gone");

        public SimulationResult KeyPress(KeyCode key) => Record($"press {key}");

        public SimulationResult KeyRelease(KeyCode key) => Record($"release {key}");

        public SimulationResult Click(MouseButton button, int x, int y) => SimulationResult.Success;

        public SimulationResult Press(MouseButton button, int x, int y) => SimulationResult.Success;

        public SimulationResult Release(MouseButton button) => SimulationResult.Success;

        public bool RepostsRemapDrags => false;

        public SimulationResult PressRemapButton(MouseButton button, KeyModifiers modifiers, int x, int y) => SimulationResult.Success;

        public SimulationResult ReleaseRemapButton(MouseButton button) => SimulationResult.Success;

        public SimulationResult DragRemapButton(MouseButton button, int x, int y, int dx, int dy) => SimulationResult.Unsupported;

        public SimulationResult MoveTo(int x, int y) => SimulationResult.Success;

        public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None) => SimulationResult.Success;

        public SimulationResult TypeText(string text) => SimulationResult.Success;

        public SimulationResult TypeTextByKeys(string text) => SimulationResult.Success;

        private SimulationResult Record(string call)
        {
            Keys.Add(call);
            return SimulationResult.Success;
        }
    }
}
