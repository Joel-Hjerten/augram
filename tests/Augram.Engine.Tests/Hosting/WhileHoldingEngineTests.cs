using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Exclusions › While holding through the whole engine with fakes (plan 0012 step 2): over a window an entry claims, a press
/// made while one of its inputs is held passes through as a held global ignore key makes it; elsewhere nothing changes; only
/// what the OS saw go down counts (decision 10); a hold remap's hold key is judged at its own press (decision 8).
/// </summary>
public sealed class WhileHoldingEngineTests
{
    private const MouseButton Stroke = EngineHarness.StrokeButton;
    private static readonly WindowIdentity AfterEffects = FakeWindowSystem.Identity("afterfx.exe", handle: 0x80);
    private static readonly WindowIdentity Notepad = FakeWindowSystem.Identity("notepad.exe", handle: 0x10);

    [Fact]
    public void AStrokePressWithAListedModifierHeld_PassesThrough_AndWithoutItIsCaptured()
    {
        using var harness = OverAfterEffects(new KeyOrButton.Modifier(KeyModifiers.Alt));

        Assert.False(harness.KeyDown(KeyCode.LeftAlt, 0, KeyModifiers.Alt));
        Assert.False(harness.Down(Stroke, 100, 100, 10, KeyModifiers.Alt), "Alt is held over After Effects: the press passes through");
        Assert.False(harness.Up(Stroke, 150, 100, 20));
        Assert.False(harness.KeyUp(KeyCode.LeftAlt, 30));
        harness.WaitForLog(LogSources.Ignore, "Press passed through: an ignoring input is held");
        var passed = Assert.Single(harness.Log.Events, e => e.Message == "Press passed through: an ignoring input is held");
        Assert.Contains(new LogProperty("by", "while holding: Alt"), passed.Properties!);

        Assert.True(harness.Down(Stroke, 100, 100, 40), "Alt is up: the press is captured");
        Assert.True(harness.Up(Stroke, 100, 100, 50));
        harness.WaitForLog(LogSources.Capture, "Click replayed");
        AssertNoMismatch(harness);
    }

    [Fact]
    public void AStrokePressWithAListedKeyHeld_PassesThrough_AndAfterItsReleaseIsCaptured()
    {
        using var harness = OverAfterEffects(new KeyOrButton.Key(KeyCode.S));

        Assert.False(harness.KeyDown(KeyCode.S, 0));
        Assert.False(harness.Down(Stroke, 100, 100, 10), "S is held over After Effects: the press passes through");
        Assert.False(harness.Up(Stroke, 100, 100, 20));
        Assert.False(harness.KeyUp(KeyCode.S, 30));

        Assert.True(harness.Down(Stroke, 100, 100, 40), "S is up: the press is captured at once");
        Assert.True(harness.Up(Stroke, 100, 100, 50));
        harness.WaitForLog(LogSources.Capture, "Click replayed");
        AssertNoMismatch(harness);
    }

    [Fact]
    public void AStrokePressWithAListedButtonHeld_PassesThrough()
    {
        using var harness = OverAfterEffects(new KeyOrButton.Button(MouseButton.Left));

        Assert.False(harness.Down(MouseButton.Left, 100, 100, 0));
        Assert.False(harness.Down(Stroke, 100, 100, 10), "Left is held over After Effects: the press passes through");
        Assert.False(harness.Up(Stroke, 100, 100, 20));
        Assert.False(harness.Up(MouseButton.Left, 100, 100, 30));

        Assert.True(harness.Down(Stroke, 100, 100, 40));
        Assert.True(harness.Up(Stroke, 100, 100, 50));
        harness.WaitForLog(LogSources.Capture, "Click replayed");
        AssertNoMismatch(harness);
    }

    [Fact]
    public void OverAnotherApp_AListedModifierHeld_ChangesNothing()
    {
        using var harness = OverAfterEffects(new KeyOrButton.Modifier(KeyModifiers.Alt));
        harness.Windows.Window = Notepad;
        harness.Move(400, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.HoldingUnderPointer.IsEmpty, "the pointer over Notepad");

        Assert.False(harness.KeyDown(KeyCode.LeftAlt, 10, KeyModifiers.Alt));
        Assert.True(harness.Down(Stroke, 400, 100, 20, KeyModifiers.Alt), "Alt lists nothing over Notepad");
        Assert.True(harness.Up(Stroke, 400, 100, 30));
        Assert.False(harness.KeyUp(KeyCode.LeftAlt, 40));
        // Alt + click binds nothing: the click is relayed with Alt, as anywhere else.
        harness.WaitForLog(LogSources.Execution, "Click relayed");
        Assert.False(harness.Log.Has(LogSources.Ignore, "Press passed through: an ignoring input is held"));
    }

    [Fact]
    public void AListedKeyWhoseReleaseWasMissed_NoLongerIgnores_AfterTheLostReleaseAge()
    {
        using var harness = OverAfterEffects(new KeyOrButton.Key(KeyCode.S));

        Assert.False(harness.KeyDown(KeyCode.S, 0));
        // No repeat and no release for longer than the record trusts: S counts as released (errs towards strokes).
        Assert.True(harness.Down(Stroke, 100, 100, KeySuppressionShadow.LostReleaseAfterMs + 500));
        Assert.True(harness.Up(Stroke, 100, 100, KeySuppressionShadow.LostReleaseAfterMs + 510));
        harness.WaitForLog(LogSources.Capture, "Click replayed");
        AssertNoMismatch(harness);
    }

    [Fact]
    public void AListedHoldKeyAugramSwallowed_DoesNotIgnore()
    {
        // D is the hold key of Blender's second hold remap (Left only): the hook swallows it, so the OS never saw it go down.
        using var harness = Blender(new KeyOrButton.Key(KeyCode.D));

        Assert.True(harness.KeyDown(KeyCode.D, 0), "D is a hold key: swallowed");
        Assert.True(harness.Down(Stroke, 100, 100, 10), "D listed, but swallowed: the stroke button is captured");
        Assert.True(harness.Up(Stroke, 100, 100, 20));
        Assert.True(harness.KeyUp(KeyCode.D, 400));
        harness.WaitForLog(LogSources.Capture, "Click replayed");
        Assert.False(harness.Log.Has(LogSources.Ignore, "Press passed through: an ignoring input is held"));
    }

    [Fact]
    public void AHoldKeyPressedWhileAListedInputIsHeld_StartsNoHoldRemap()
    {
        using var harness = Blender(new KeyOrButton.Modifier(KeyModifiers.Alt));

        Assert.False(harness.KeyDown(KeyCode.LeftAlt, 0, KeyModifiers.Alt));
        Assert.False(harness.KeyDown(KeyCode.Space, 10, KeyModifiers.Alt), "Alt is held in Blender: Space reaches the app");
        Assert.False(harness.Down(MouseButton.Left, 100, 100, 20, KeyModifiers.Alt), "no hold remap: Left is Blender's");
        Assert.False(harness.Up(MouseButton.Left, 100, 100, 30));
        Assert.False(harness.KeyUp(KeyCode.Space, 40, KeyModifiers.Alt));
        Assert.False(harness.KeyUp(KeyCode.LeftAlt, 50));

        Assert.Empty(harness.Simulator.All);
        Assert.Equal(0, harness.Host.PendingHoldReplays);
    }

    [Fact]
    public void ARunningHoldRemap_IsNotStoppedByAListedInputPressedAfterItsHoldKey()
    {
        using var harness = Blender(new KeyOrButton.Modifier(KeyModifiers.Alt));

        Assert.True(harness.KeyDown(KeyCode.Space, 0), "Space first: the hold remap starts");
        harness.KeyDown(KeyCode.LeftAlt, 10, KeyModifiers.Alt);
        Assert.True(harness.Down(MouseButton.Left, 100, 100, 20, KeyModifiers.Alt), "Orbit still runs with Alt pressed after Space");
        Assert.True(harness.Up(MouseButton.Left, 100, 100, 30));
        harness.KeyUp(KeyCode.LeftAlt, 40);
        Assert.True(harness.KeyUp(KeyCode.Space, 60));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Contains("down Middle@100,100", harness.Simulator.All);
        Assert.Contains("up Middle", harness.Simulator.All);
    }

    private static EngineHarness OverAfterEffects(KeyOrButton input)
    {
        var mapping = MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal], [Entry("After Effects", "afterfx.exe", input)]));
        var harness = new EngineHarness(mapping: mapping);
        harness.Windows.Window = AfterEffects;
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => !harness.Host.HoldingUnderPointer.IsEmpty, "the While holding inputs over After Effects");
        return harness;
    }

    /// <summary>Joel's Blender hold remaps, Blender in front and under the pointer, with a While holding entry for Blender listing <paramref name="input"/>.</summary>
    private static EngineHarness Blender(KeyOrButton input)
    {
        var mapping = MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal, BlenderHold.Group()], [Entry("Blender", BlenderHold.Process, input)]));
        var harness = new EngineHarness(mapping: mapping);
        harness.Windows.Window = BlenderHold.Window;
        harness.Windows.ForegroundWindow = BlenderHold.Window;
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => !harness.Host.ForegroundHoldPlan.IsEmpty && !harness.Host.ForegroundHolding.IsEmpty && !harness.Host.HoldingUnderPointer.IsEmpty, "Blender's hold remaps and While holding inputs");
        return harness;
    }

    private static IgnoredApp Entry(string name, string process, KeyOrButton input)
        => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = [process] }, DisableEntirely: false)
        {
            Scope = IgnoreScope.WhileHolding,
            Holding = new PlatformList<WhileHoldingInput>([new WhileHoldingInput(input, HostPlatform.Windows)], null),
        };

    private static void AssertNoMismatch(EngineHarness harness)
    {
        Assert.False(harness.Log.Has(LogSources.Capture, "Suppression decision mismatch"));
        Assert.False(harness.Log.Has(LogSources.Hold, "Hold decision mismatch"));
    }
}
