using Augram.App.Components.HotkeyCapture;
using Augram.Core.Abstractions;
using Avalonia.Input;
using Xunit;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;
using UiModifiers = Avalonia.Input.KeyModifiers;

namespace Augram.App.Tests.Steps;

public sealed class HotkeyRecorderTests
{
    [Fact]
    public void ModifiersAreHeldUntilAKeyCompletesTheCombination()
    {
        var recorder = new HotkeyRecorder();
        Assert.Null(recorder.LiveText);

        recorder.Down(KeyCode.LeftControl, KeyModifiers.None);
        recorder.Down(KeyCode.RightShift, KeyModifiers.Control);
        Assert.Equal("Ctrl+Shift+…", recorder.LiveText);
        Assert.False(recorder.HasCombination);

        recorder.Down(KeyCode.T, KeyModifiers.None);
        recorder.Up(KeyCode.T);
        recorder.Up(KeyCode.LeftControl);

        Assert.Equal((KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T), (recorder.Modifiers, recorder.Key));
        Assert.Equal("Ctrl+Shift+T", recorder.LiveText);
        Assert.Equal(KeyModifiers.Shift, recorder.Held);
    }

    [Fact]
    public void TheLatestCombinationWinsAndTheReportedMaskCoversModifiersHeldBeforeCapture()
    {
        var recorder = new HotkeyRecorder();
        recorder.Down(KeyCode.W, KeyModifiers.Meta);
        Assert.Equal("Win+W", recorder.LiveText);

        recorder.Down(KeyCode.Escape, KeyModifiers.None);
        Assert.Equal("Esc", recorder.LiveText);

        recorder.Down(KeyCode.None, KeyModifiers.Alt);
        Assert.Equal("Esc", recorder.LiveText);

        recorder.Reset();
        Assert.Null(recorder.LiveText);
        Assert.Equal(KeyModifiers.None, recorder.Held);
    }

    [Fact]
    public void ALoneModifierNeverBecomesTheKey()
    {
        var recorder = new HotkeyRecorder();
        recorder.Down(KeyCode.LeftMeta, KeyModifiers.None);
        recorder.Up(KeyCode.LeftMeta);

        Assert.False(recorder.HasCombination);
        Assert.Null(recorder.LiveText);
    }

    [Theory]
    [InlineData(PhysicalKey.T, KeyCode.T)]
    [InlineData(PhysicalKey.Digit5, KeyCode.Digit5)]
    [InlineData(PhysicalKey.F12, KeyCode.F12)]
    [InlineData(PhysicalKey.Escape, KeyCode.Escape)]
    [InlineData(PhysicalKey.ArrowLeft, KeyCode.Left)]
    [InlineData(PhysicalKey.ControlRight, KeyCode.RightControl)]
    [InlineData(PhysicalKey.MetaLeft, KeyCode.LeftMeta)]
    [InlineData(PhysicalKey.Backquote, KeyCode.BackQuote)]
    [InlineData(PhysicalKey.Equal, KeyCode.Equals)]
    [InlineData(PhysicalKey.NumPadEnter, KeyCode.NumPadEnter)]
    [InlineData(PhysicalKey.MediaPlayPause, KeyCode.MediaPlay)]
    [InlineData(PhysicalKey.AudioVolumeUp, KeyCode.VolumeUp)]
    [InlineData(PhysicalKey.IntlBackslash, KeyCode.None)]
    [InlineData(PhysicalKey.None, KeyCode.None)]
    public void WindowKeysMapByPhysicalPosition(PhysicalKey physical, KeyCode expected)
    {
        Assert.Equal(expected, AvaloniaKeyMap.ToKeyCode(physical));
    }

    [Fact]
    public void EveryCoreKeyIsReachableFromAPhysicalKey()
    {
        var reachable = Enum.GetValues<PhysicalKey>().Select(AvaloniaKeyMap.ToKeyCode).ToHashSet();

        Assert.DoesNotContain(Enum.GetValues<KeyCode>(), key => key != KeyCode.None && !reachable.Contains(key));
    }

    [Fact]
    public void WindowModifierBitsAreTranslated()
    {
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Meta, AvaloniaKeyMap.ToModifiers(UiModifiers.Control | UiModifiers.Meta));
        Assert.Equal(KeyModifiers.Alt | KeyModifiers.Shift, AvaloniaKeyMap.ToModifiers(UiModifiers.Alt | UiModifiers.Shift));
        Assert.Equal(KeyModifiers.None, AvaloniaKeyMap.ToModifiers(UiModifiers.None));
    }
}
