using Augram.App.Components.HotkeyCapture;
using Augram.Core.Abstractions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;

namespace Augram.App.Tests.Steps;

/// <summary>
/// The capture field's one-key mode (plan 0002 step 4: a hold key, a hold remap command's key input): one key, no modifiers;
/// a modifier is refused and the field says why (the host's reason when it gives one); a key the host refuses is not taken.
/// Uses <see cref="FakeKeyCapture"/>; nothing is hooked.
/// </summary>
public sealed class HotkeyCaptureBoxSingleKeyTests
{
    [AvaloniaFact]
    public void OneKeyModeTakesTheKeyAloneAndRefusesAModifierSayingWhy()
    {
        var capture = new FakeKeyCapture();
        var box = Show(capture);
        var committed = new List<HotkeyCommittedEventArgs>();
        box.Committed += (_, e) => committed.Add(e);

        box.BeginCapture();
        Assert.Equal(HotkeyCaptureBox.PromptKeyText, box.DisplayText);
        capture.Press(KeyCode.LeftShift);

        Assert.Equal($"Left Shift cannot be used here: this field takes one key, not {HotkeyCaptureBox.ModifierWords()}.", box.StatusText);
        Assert.Equal(HotkeyCaptureBox.PromptKeyText, box.DisplayText);

        capture.Press(KeyCode.G, KeyModifiers.Shift);
        Assert.Equal("G", box.DisplayText);
        Assert.Equal(HotkeyCaptureBox.EngineHelpText, box.StatusText);
        box.Accept();

        var kept = Assert.Single(committed);
        Assert.Equal((KeyModifiers.None, KeyCode.G, KeyModifiers.None), (kept.Modifiers, kept.Key, kept.RightHand));
        Assert.Equal("G", box.DisplayText);
        Assert.False(capture.IsArmed);
    }

    [AvaloniaFact]
    public void AKeyTheHostRefusesIsNotTaken_ItsReasonShows()
    {
        var capture = new FakeKeyCapture();
        var box = Show(capture);
        box.KeyProblem = key => key == KeyCode.Space ? "Space is taken." : null;
        box.Key = KeyCode.W;

        box.BeginCapture();
        capture.Press(KeyCode.Space);
        Assert.Equal("Space is taken.", box.StatusText);
        box.Accept();

        Assert.Equal(KeyCode.W, box.Key);
        Assert.Equal("Space is taken.", box.Refusal(KeyCode.Space));
        Assert.Null(box.Refusal(KeyCode.Q));
        Assert.NotNull(box.Refusal(KeyCode.RightMeta));
    }

    private static HotkeyCaptureBox Show(IKeyCapture capture)
    {
        var box = new HotkeyCaptureBox { KeyCapture = capture, SingleKey = true };
        var window = new Window { Width = 600, Height = 300 };
        window.Resources.MergedDictionaries.Add(HotkeyCaptureBoxTests.Theme());
        window.Content = box;
        window.Show();
        return box;
    }
}
