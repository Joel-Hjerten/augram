using Augram.Core.Abstractions;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>A press is suppressed iff capture is armed when it starts; its repeats and its release follow it, whatever capture does in between (A19 for keys).</summary>
public sealed class KeySuppressionShadowTests
{
    private readonly KeySuppressionShadow _shadow = new();

    [Fact]
    public void NothingIsSuppressedWhileNotCapturing()
    {
        Assert.False(Down(KeyCode.T, 0, capturing: false));
        Assert.False(Down(KeyCode.T, 30, capturing: false));
        Assert.False(Up(KeyCode.T, 60, capturing: false));
        Assert.Equal(0, _shadow.OwedCount);
    }

    [Fact]
    public void APressDuringCaptureIsSwallowedWithItsRepeatsAndRelease()
    {
        Assert.True(Down(KeyCode.LeftControl, 0, capturing: true));
        Assert.True(Down(KeyCode.T, 10, capturing: true));
        Assert.True(Down(KeyCode.T, 40, capturing: true));
        Assert.True(_shadow.IsOwed(KeyCode.T));
        Assert.True(Up(KeyCode.T, 50, capturing: true));
        Assert.True(Up(KeyCode.LeftControl, 60, capturing: true));
        Assert.Equal(0, _shadow.OwedCount);
    }

    [Fact]
    public void AnOwedReleaseIsStillSwallowedAfterCaptureEnds()
    {
        Assert.True(Down(KeyCode.LeftShift, 0, capturing: true));

        Assert.True(Down(KeyCode.LeftShift, 500, capturing: false));
        Assert.True(Up(KeyCode.LeftShift, 600, capturing: false));

        Assert.False(Down(KeyCode.LeftShift, 700, capturing: false));
        Assert.False(Up(KeyCode.LeftShift, 710, capturing: false));
    }

    [Fact]
    public void AKeyHeldFromBeforeCaptureReachesTheOsToItsRelease()
    {
        Assert.False(Down(KeyCode.LeftAlt, 0, capturing: false));

        Assert.False(Down(KeyCode.LeftAlt, 500, capturing: true));
        Assert.False(Up(KeyCode.LeftAlt, 600, capturing: true));

        Assert.True(Down(KeyCode.LeftAlt, 700, capturing: true));
        Assert.True(Up(KeyCode.LeftAlt, 710, capturing: true));
    }

    [Fact]
    public void AStateOlderThanTheLostReleaseWindowStartsFresh()
    {
        const long Later = KeySuppressionShadow.LostReleaseAfterMs + 1;

        // An owed key whose release the hook never saw: the next press after the window passes through.
        Assert.True(Down(KeyCode.LeftControl, 0, capturing: true));
        Assert.False(Down(KeyCode.LeftControl, Later, capturing: false));
        Assert.False(Up(KeyCode.LeftControl, Later + 10, capturing: false));

        // A passed key whose release was missed: a press during a later capture is swallowed.
        Assert.False(Down(KeyCode.LeftMeta, 0, capturing: false));
        Assert.True(Down(KeyCode.LeftMeta, Later, capturing: true));
        Assert.True(Up(KeyCode.LeftMeta, Later + 10, capturing: false));
    }

    [Fact]
    public void ResetForgetsOwedKeysAndOtherInputIsNeverSuppressed()
    {
        Assert.True(Down(KeyCode.Escape, 0, capturing: true));
        _shadow.Reset();

        Assert.False(Up(KeyCode.Escape, 10, capturing: false));
        Assert.False(_shadow.Decide(RawInput.ButtonDown(Core.Capture.MouseButton.Right, 0, 0, 20), captureArmed: true));
        Assert.False(_shadow.Decide(RawInput.Move(1, 1, 30), captureArmed: true));
    }

    [Fact]
    public void KeysWithoutACoreNameShareOneSlotAndStillPair()
    {
        Assert.True(Down(KeyCode.None, 0, capturing: true));
        Assert.True(Up(KeyCode.None, 10, capturing: false));
        Assert.False(Down(KeyCode.None, 20, capturing: false));
    }

    private bool Down(KeyCode key, long t, bool capturing) => _shadow.Decide(RawInput.KeyDown(key, t), capturing);

    private bool Up(KeyCode key, long t, bool capturing) => _shadow.Decide(RawInput.KeyUp(key, t), capturing);
}
