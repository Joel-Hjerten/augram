using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>F8 both ways, best effort (Joel, 2026-10-07): the modifier swap, the exception table, and the combinations with no guess.</summary>
public sealed class HotkeyConversionTests
{
    private const KeyModifiers Ctrl = KeyModifiers.Control;
    private const KeyModifiers Alt = KeyModifiers.Alt;
    private const KeyModifiers Shift = KeyModifiers.Shift;
    private const KeyModifiers Meta = KeyModifiers.Meta;
    private const KeyModifiers None = KeyModifiers.None;

    [Theory]
    [InlineData(Ctrl, KeyCode.W, None, Meta, KeyCode.W, None)]
    [InlineData(Ctrl | Shift, KeyCode.T, None, Meta | Shift, KeyCode.T, None)]
    [InlineData(Ctrl, KeyCode.Digit0, Ctrl, Meta, KeyCode.Digit0, Meta)]
    [InlineData(Ctrl | Alt, KeyCode.F10, Alt, Meta | Alt, KeyCode.F10, Alt)]
    [InlineData(Alt, KeyCode.Tab, None, Meta, KeyCode.Tab, None)]
    [InlineData(Alt | Shift, KeyCode.Tab, None, Meta | Shift, KeyCode.Tab, None)]
    [InlineData(Alt, KeyCode.F4, None, Meta, KeyCode.W, None)]
    [InlineData(Ctrl, KeyCode.Y, None, Meta | Shift, KeyCode.Z, None)]
    public void WindowsToMac_Converts(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand, KeyModifiers expectedModifiers, KeyCode expectedKey, KeyModifiers expectedRightHand)
    {
        var result = Convert(new HotkeyStep(modifiers, key, rightHand), HostPlatform.Windows, HostPlatform.MacOS);

        Assert.Equal(StepConversionKind.Converted, result.Kind);
        Assert.Equal(new HotkeyStep(expectedModifiers, expectedKey, expectedRightHand), result.Step);
    }

    [Theory]
    [InlineData(Meta, KeyCode.W, None, Ctrl, KeyCode.W, None)]
    [InlineData(Meta, KeyCode.C, Meta, Ctrl, KeyCode.C, Ctrl)]
    [InlineData(Meta | Shift, KeyCode.Z, None, Ctrl, KeyCode.Y, None)]
    [InlineData(Meta, KeyCode.Tab, None, Alt, KeyCode.Tab, None)]
    [InlineData(Meta, KeyCode.Q, None, Alt, KeyCode.F4, None)]
    public void MacToWindows_Converts(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand, KeyModifiers expectedModifiers, KeyCode expectedKey, KeyModifiers expectedRightHand)
    {
        var result = Convert(new HotkeyStep(modifiers, key, rightHand), HostPlatform.MacOS, HostPlatform.Windows);

        Assert.Equal(StepConversionKind.Converted, result.Kind);
        Assert.Equal(new HotkeyStep(expectedModifiers, expectedKey, expectedRightHand), result.Step);
    }

    [Theory]
    [InlineData(HostPlatform.Windows, None, KeyCode.F5)]
    [InlineData(HostPlatform.Windows, Shift, KeyCode.F5)]
    [InlineData(HostPlatform.Windows, Ctrl, KeyCode.Tab)]
    [InlineData(HostPlatform.Windows, Ctrl | Shift, KeyCode.Tab)]
    [InlineData(HostPlatform.Windows, Alt, KeyCode.F9)]
    [InlineData(HostPlatform.MacOS, Ctrl, KeyCode.Tab)]
    [InlineData(HostPlatform.MacOS, Alt, KeyCode.Left)]
    [InlineData(HostPlatform.MacOS, None, KeyCode.Space)]
    public void WhatTheRulesLeaveAsItWas_IsUnchanged(HostPlatform from, KeyModifiers modifiers, KeyCode key)
    {
        var step = new HotkeyStep(modifiers, key);

        var result = Convert(step, from, Other(from));

        Assert.Equal(StepConversionKind.Unchanged, result.Kind);
        Assert.Same(step, result.Step);
    }

    [Fact]
    public void TheWindowsKeyHasNoMacGuess()
    {
        var result = Convert(new HotkeyStep(Meta, KeyCode.D), HostPlatform.Windows, HostPlatform.MacOS);

        Assert.Equal(StepConversionKind.NotConvertible, result.Kind);
        Assert.Null(result.Step);
        Assert.StartsWith("Win+D needs a macOS version", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CmdAndCtrlTogetherHaveNoWindowsGuess()
    {
        var result = Convert(new HotkeyStep(Ctrl | Meta, KeyCode.F), HostPlatform.MacOS, HostPlatform.Windows);

        Assert.Equal(StepConversionKind.NotConvertible, result.Kind);
        Assert.StartsWith("Ctrl+Cmd+F needs a Windows version", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SamePlatformAndUnsetStepsAreUnchanged()
    {
        var step = new HotkeyStep(Meta, KeyCode.D);

        Assert.Same(step, Convert(step, HostPlatform.Windows, HostPlatform.Windows).Step);
        Assert.Equal(StepConversionKind.Unchanged, Convert(HotkeyStep.Unset, HostPlatform.Windows, HostPlatform.MacOS).Kind);
    }

    [Fact]
    public void ASwappedHotkeyComesBackTheSameWay()
    {
        var windows = new HotkeyStep(Ctrl | Shift, KeyCode.T);

        var mac = (HotkeyStep)Convert(windows, HostPlatform.Windows, HostPlatform.MacOS).Step!;
        var back = Convert(mac, HostPlatform.MacOS, HostPlatform.Windows).Step;

        Assert.Equal(windows, back);
    }

    [Fact]
    public void SummaryOn_ReadsInThatPlatformsNames()
    {
        var step = new HotkeyStep(Meta | Alt, KeyCode.D);

        Assert.Equal("Alt+Win+D", step.SummaryOn(HostPlatform.Windows));
        Assert.Equal("Opt+Cmd+D", step.SummaryOn(HostPlatform.MacOS));
    }

    [Fact]
    public void ACommandStepRunsItsOverride_ItselfWhereAuthored_ElseTheGuess()
    {
        var ctrlW = new HotkeyStep(Ctrl, KeyCode.W);
        var authored = new CommandStep(ctrlW, HostPlatform.Windows);

        Assert.Same(ctrlW, authored.ForPlatform(HostPlatform.Windows).Step);
        Assert.Equal(new HotkeyStep(Meta, KeyCode.W), authored.ForPlatform(HostPlatform.MacOS).Step);

        var own = new HotkeyStep(Meta | Shift, KeyCode.W);
        var withOverride = authored with { MacOsOverride = own };
        var resolved = withOverride.ForPlatform(HostPlatform.MacOS);
        Assert.Equal(StepConversionKind.Unchanged, resolved.Kind);
        Assert.Same(own, resolved.Step);
    }

    private static StepConversion Convert(HotkeyStep step, HostPlatform from, HostPlatform to) => HotkeyStepType.Instance.Convert(step, from, to);

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.Windows ? HostPlatform.MacOS : HostPlatform.Windows;
}
