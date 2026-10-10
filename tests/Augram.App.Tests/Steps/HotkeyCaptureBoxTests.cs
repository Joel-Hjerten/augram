using System.Reflection;
using Augram.App.Components.HotkeyCapture;
using Augram.Core.Abstractions;
using Augram.Engine.Hosting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Xunit;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;
using MouseButton = Avalonia.Input.MouseButton;

namespace Augram.App.Tests.Steps;

/// <summary>
/// F5's capture field: arms the capture service, shows the combination live, commits only by mouse
/// (Accept, a press outside) or when the engine ends it, gives the keyboard back on every way out, and
/// falls back to window keys without an engine. Uses <see cref="FakeKeyCapture"/>; nothing is hooked.
/// </summary>
public sealed class HotkeyCaptureBoxTests
{
    [AvaloniaFact]
    public void ShowsTheCombinationOrNoKeySet()
    {
        var (box, _, _) = Show(new FakeKeyCapture());
        Assert.Equal(HotkeyCaptureBox.EmptyText, box.DisplayText);

        box.Modifiers = KeyModifiers.Control | KeyModifiers.Shift;
        box.Key = KeyCode.T;

        Assert.Equal("Ctrl+Shift+T", box.DisplayText);
        Assert.False(Part<Button>(box, "PART_Accept").IsVisible);
        Assert.True(Part<Button>(box, "PART_Capture").IsVisible);

        box.RightHand = KeyModifiers.Shift;
        Assert.Equal("Ctrl+RShift+T", box.DisplayText);
    }

    [AvaloniaFact]
    public void ARightHandCombinationCommitsWithItsSide_ClearDropsIt()
    {
        var capture = new FakeKeyCapture();
        var (box, _, _) = Show(capture);
        var committed = new List<HotkeyCommittedEventArgs>();
        box.Committed += (_, e) => committed.Add(e);
        box.BeginCapture();

        capture.Press(KeyCode.RightAlt);
        Assert.Equal("RAlt+…", box.DisplayText);
        capture.Press(KeyCode.F9, KeyModifiers.Alt);
        Assert.Equal("RAlt+F9", box.DisplayText);
        box.Accept();

        var kept = Assert.Single(committed);
        Assert.Equal((KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), (kept.Modifiers, kept.Key, kept.RightHand));
        Assert.Equal((KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), (box.Modifiers, box.Key, box.RightHand));
        Assert.Equal("RAlt+F9", box.DisplayText);

        box.Clear();

        Assert.Equal((KeyModifiers.None, KeyCode.None, KeyModifiers.None), (committed[^1].Modifiers, committed[^1].Key, committed[^1].RightHand));
        Assert.Equal(KeyModifiers.None, box.RightHand);
    }

    [AvaloniaFact]
    public void CapturingTakesTheKeyboardShowsKeysLiveAndAcceptCommits()
    {
        var capture = new FakeKeyCapture();
        var (box, _, _) = Show(capture);
        var committed = Committed(box);

        Click(Part<Button>(box, "PART_Capture"));

        Assert.True(box.IsCapturing);
        Assert.True(capture.IsArmed);
        Assert.Equal(TimeSpan.FromSeconds(10), capture.LastIdleTimeout);
        Assert.Contains(":capturing", box.Classes);
        Assert.True(Part<Button>(box, "PART_Accept").IsVisible);
        Assert.Equal(HotkeyCaptureBox.PromptText, box.DisplayText);
        Assert.Equal(HotkeyCaptureBox.EngineHelpText, box.StatusText);

        capture.Press(KeyCode.LeftControl);
        Assert.Equal("Ctrl+…", box.DisplayText);
        capture.Press(KeyCode.Escape, KeyModifiers.Control);
        capture.Release(KeyCode.Escape);
        Assert.Equal("Ctrl+Esc", box.DisplayText);

        Click(Part<Button>(box, "PART_Accept"));

        Assert.False(capture.IsArmed);
        Assert.False(box.IsCapturing);
        Assert.Equal([(KeyModifiers.Control, KeyCode.Escape)], committed);
        Assert.Equal((KeyModifiers.Control, KeyCode.Escape), (box.Modifiers, box.Key));
        Assert.Equal("Ctrl+Esc", box.DisplayText);
        Assert.False(box.HasStatus);
    }

    [AvaloniaFact]
    public void APressOutsideTheFieldCommits_APressInsideDoesNot()
    {
        var capture = new FakeKeyCapture();
        var (box, other, window) = Show(capture);
        var committed = Committed(box);
        box.BeginCapture();
        capture.Press(KeyCode.F5, KeyModifiers.Alt);

        PressAt(window, Part<Border>(box, "PART_Field"));
        Assert.True(box.IsCapturing);
        Assert.Empty(committed);

        PressAt(window, other);

        Assert.False(box.IsCapturing);
        Assert.False(capture.IsArmed);
        Assert.Equal([(KeyModifiers.Alt, KeyCode.F5)], committed);
    }

    [AvaloniaFact]
    public void TheWindowLosingActivationCommits()
    {
        var capture = new FakeKeyCapture();
        var (box, _, window) = Show(capture);
        var committed = Committed(box);
        box.BeginCapture();
        capture.Press(KeyCode.Tab, KeyModifiers.Alt);

        RaiseDeactivated(window);

        Assert.False(box.IsCapturing);
        Assert.False(capture.IsArmed);
        Assert.Equal([(KeyModifiers.Alt, KeyCode.Tab)], committed);
    }

    [AvaloniaFact]
    public void AcceptWithoutAKeyKeepsTheOldValue()
    {
        var capture = new FakeKeyCapture();
        var (box, _, _) = Show(capture);
        box.Key = KeyCode.W;
        var committed = Committed(box);
        box.BeginCapture();
        capture.Press(KeyCode.LeftShift);

        box.Accept();

        Assert.False(capture.IsArmed);
        Assert.Empty(committed);
        Assert.Equal("W", box.DisplayText);
    }

    [AvaloniaFact]
    public void UnloadingGivesTheKeyboardBackAndKeepsNothing()
    {
        var capture = new FakeKeyCapture();
        var (box, _, window) = Show(capture);
        var committed = Committed(box);
        box.BeginCapture();
        capture.Press(KeyCode.T, KeyModifiers.Control);

        ((Panel)window.Content!).Children.Remove(box);

        Assert.False(capture.IsArmed);
        Assert.False(box.IsCapturing);
        Assert.Empty(committed);
    }

    [AvaloniaFact]
    public void TheEnginesReleaseKeepsWhatWasPressedAndSaysWhy()
    {
        var capture = new FakeKeyCapture();
        var (box, _, _) = Show(capture);
        var committed = Committed(box);
        box.BeginCapture();
        capture.Press(KeyCode.PrintScreen, KeyModifiers.Meta);

        capture.EngineReleases(KeyCaptureEvent.IdleTimeoutReason);

        Assert.False(box.IsCapturing);
        Assert.Equal([(KeyModifiers.Meta, KeyCode.PrintScreen)], committed);
        Assert.Equal("Capture stopped after 10 s without a key.", box.StatusText);
        Assert.True(box.HasStatus);
    }

    [AvaloniaFact]
    public void AStaleReleaseFromAnEarlierCaptureIsIgnored()
    {
        var capture = new FakeKeyCapture();
        var (box, _, _) = Show(capture);
        box.BeginCapture();
        box.Accept();
        box.BeginCapture();

        capture.EngineReleases(KeyCaptureEvent.ReplacedReason, session: 0);

        Assert.True(box.IsCapturing);
        Assert.True(capture.IsArmed);
        box.Accept();
    }

    [AvaloniaFact]
    public void ClearCommitsNoKeyAndEndsACapture()
    {
        var capture = new FakeKeyCapture();
        var (box, _, _) = Show(capture);
        box.Modifiers = KeyModifiers.Alt;
        box.Key = KeyCode.F4;
        var committed = Committed(box);
        box.BeginCapture();

        Click(Part<Button>(box, "PART_Clear"));

        Assert.False(capture.IsArmed);
        Assert.Equal([(KeyModifiers.None, KeyCode.None)], committed);
        Assert.Equal(HotkeyCaptureBox.EmptyText, box.DisplayText);
    }

    [AvaloniaFact]
    public void WithoutAnEngineItReadsTheWindowsKeysAndSaysSo()
    {
        var (box, _, window) = Show(new FakeKeyCapture { Available = false });
        var committed = Committed(box);
        box.BeginCapture();

        Assert.True(box.IsWindowOnly);
        Assert.Equal(HotkeyCaptureBox.WindowOnlyText, box.StatusText);
        window.KeyPressQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Equal("Shift+Tab", box.DisplayText);

        box.Accept();

        Assert.Equal([(KeyModifiers.Shift, KeyCode.Tab)], committed);
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
        Assert.Equal("Shift+Tab", box.DisplayText);
    }

    [AvaloniaFact]
    public void TheServiceIsFoundAsAResourceWhenNotSetDirectly()
    {
        var capture = new FakeKeyCapture();
        var (box, _, window) = Show(null);
        window.Resources[HotkeyCaptureBox.KeyCaptureResourceKey] = capture;

        box.BeginCapture();

        Assert.True(capture.IsArmed);
        Assert.False(box.IsWindowOnly);
        box.Accept();
    }

    internal static ResourceDictionary Theme() => (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://Augram/Themes/Wireframe/Hotkey.axaml"));

    private static (HotkeyCaptureBox Box, Button Other, Window Window) Show(IKeyCapture? capture)
    {
        var box = new HotkeyCaptureBox { KeyCapture = capture };
        var other = new Button { Content = "Elsewhere", Width = 120, Height = 40 };
        // The theme goes in before the content: a control picks its implicit ControlTheme when it joins the tree.
        var window = new Window { Width = 600, Height = 300 };
        window.Resources.MergedDictionaries.Add(Theme());
        window.Content = new StackPanel { Children = { box, other } };
        window.Show();
        window.UpdateLayout();
        return (box, other, window);
    }

    private static List<(KeyModifiers, KeyCode)> Committed(HotkeyCaptureBox box)
    {
        var committed = new List<(KeyModifiers, KeyCode)>();
        box.Committed += (_, e) => committed.Add((e.Modifiers, e.Key));
        return committed;
    }

    private static T Part<T>(TemplatedControl control, string name)
        where T : Control
        => control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    /// <summary>Headless windows never lose activation, so raise <see cref="WindowBase.Deactivated"/> as the platform would when another app takes over.</summary>
    private static void RaiseDeactivated(Window window)
    {
        var handlers = typeof(WindowBase).GetField(nameof(WindowBase.Deactivated), BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WindowBase.Deactivated is no longer a field-like event; raise it another way.");
        ((EventHandler?)handlers.GetValue(window))?.Invoke(window, EventArgs.Empty);
    }

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static void PressAt(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(3, 3), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
