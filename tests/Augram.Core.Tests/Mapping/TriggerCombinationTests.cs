using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Trigger combinations in the mapping (F1, Joel 2026-10-09; learnings 0003 §3–4): exact matching with Before / After /
/// Either, no fallback to fewer keys at either level, anchors other than the stroke button, the A7 overlap rule, and a
/// synced trigger's keys converted like a hotkey's.
/// </summary>
public sealed class TriggerCombinationTests
{
    private const MouseButton Stroke = MouseButton.Right;

    private static PressHold Press(KeyModifiers before = KeyModifiers.None, KeyModifiers after = KeyModifiers.None, HeldButtons beforeButtons = HeldButtons.None, HeldButtons afterButtons = HeldButtons.None)
        => new(HeldButtons.Stroke, Stroke, beforeButtons, before, afterButtons, after);

    [Theory]
    [InlineData(HoldCapture.Either, KeyModifiers.Shift, KeyModifiers.None, true)]
    [InlineData(HoldCapture.Either, KeyModifiers.None, KeyModifiers.Shift, true)]
    [InlineData(HoldCapture.Before, KeyModifiers.Shift, KeyModifiers.None, true)]
    [InlineData(HoldCapture.Before, KeyModifiers.None, KeyModifiers.Shift, false)]
    [InlineData(HoldCapture.After, KeyModifiers.None, KeyModifiers.Shift, true)]
    [InlineData(HoldCapture.After, KeyModifiers.Shift, KeyModifiers.None, false)]
    [InlineData(HoldCapture.Either, KeyModifiers.Shift, KeyModifiers.Control, false)]
    [InlineData(HoldCapture.Either, KeyModifiers.None, KeyModifiers.None, false)]
    public void ShiftMatchesExactly_InItsCaptureMode(HoldCapture capture, KeyModifiers before, KeyModifiers after, bool matches)
    {
        Assert.Equal(matches, TriggerHold.WithStroke(KeyModifiers.Shift, capture: capture).Matches(Press(before, after)));
    }

    [Fact]
    public void APlainTrigger_DoesNotFireWithAKeyHeld()
    {
        Assert.True(TriggerHold.Default.Matches(Press()));
        Assert.False(TriggerHold.Default.Matches(Press(before: KeyModifiers.Shift)));
        Assert.False(TriggerHold.Default.Matches(Press(afterButtons: HeldButtons.Left)));
    }

    [Fact]
    public void ShiftBeforeAndCtrlAfter_MatchOnlyTheEitherTriggerHoldingBoth()
    {
        var press = Press(before: KeyModifiers.Shift, after: KeyModifiers.Control);

        Assert.True(TriggerHold.WithStroke(KeyModifiers.Control | KeyModifiers.Shift).Matches(press));
        Assert.False(TriggerHold.WithStroke(KeyModifiers.Shift, capture: HoldCapture.Before).Matches(press));
        Assert.False(TriggerHold.WithStroke(KeyModifiers.Control, capture: HoldCapture.After).Matches(press));
    }

    [Fact]
    public void AnAnchorTrigger_MatchesAPressOwnedByAnyOfItsButtons_NeverTheStrokeButton()
    {
        var rightAndX1 = new TriggerHold(HeldButtons.Right | HeldButtons.X1);

        Assert.True(rightAndX1.Matches(new PressHold(HeldButtons.Right, MouseButton.Middle, After: HeldButtons.X1)));
        Assert.True(rightAndX1.Matches(new PressHold(HeldButtons.X1, MouseButton.Middle, After: HeldButtons.Right)));
        Assert.False(rightAndX1.Matches(new PressHold(HeldButtons.Right, MouseButton.Middle)));
        Assert.False(new TriggerHold(HeldButtons.Right).Matches(new PressHold(HeldButtons.Stroke, MouseButton.Middle)));
    }

    [Fact]
    public void ATriggerNamingTheStrokeButton_IsTheStrokeButtonOnThatMachine()
    {
        var middle = new TriggerHold(HeldButtons.Middle);

        Assert.True(middle.Matches(new PressHold(HeldButtons.Stroke, MouseButton.Middle)));
        Assert.True(middle.Matches(new PressHold(HeldButtons.Middle, MouseButton.Right)));
    }

    /// <summary>Learnings 0003 §2.2's worked cases: Chrome plain Up, Global Shift + Up; no fallback at either level.</summary>
    [Fact]
    public void TheResolverMatchesExactly_AndAPlainAppOverrideDoesNotShadowAGlobalCombination()
    {
        var globalShiftUp = NewCommand("Shift Up", Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Shift)), NewStep("global"));
        var chromeUp = NewCommand("Close tab", Trigger.ForGesture(Up), NewStep("chrome"));
        var mapping = Document(NewGlobal(globalShiftUp), NewGroup("Chrome", null, chromeUp));
        PressedTrigger Pressed(KeyModifiers before) => new(Trigger.ForGesture(Up), Press(before));

        Assert.Equal("Close tab", CommandResolver.Resolve(mapping, Window("chrome.exe"), Pressed(KeyModifiers.None), HostPlatform.Windows).Command?.Name);
        Assert.Equal("Shift Up", CommandResolver.Resolve(mapping, Window("chrome.exe"), Pressed(KeyModifiers.Shift), HostPlatform.Windows).Command?.Name);
        var alt = CommandResolver.Resolve(mapping, Window("chrome.exe"), Pressed(KeyModifiers.Alt), HostPlatform.Windows);
        Assert.Equal(ResolutionOutcome.None, alt.Outcome);
        Assert.Equal("no command for Alt + this gesture", alt.Reason);
    }

    [Fact]
    public void RightPlusWheel_IsItsOwnTriggerWhereMiddleStrokes_AndTheStrokeButtonWhereRightDoes()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Zoom", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), NewStep("zoom")), NewCommand("Volume", Trigger.ForWheel(WheelDirection.Up), NewStep("volume")))));
        PressedTrigger Tick(HeldButtons anchor, MouseButton stroke) => new(Trigger.ForWheel(WheelDirection.Up), new PressHold(anchor, stroke));

        Assert.Equal("Zoom", CommandResolver.Resolve(mapping, null, Tick(HeldButtons.Right, MouseButton.Middle), HostPlatform.Windows).Command?.Name);
        Assert.Equal("Volume", CommandResolver.Resolve(mapping, null, Tick(HeldButtons.Stroke, MouseButton.Middle), HostPlatform.Windows).Command?.Name);
        Assert.Equal("Zoom", CommandResolver.Resolve(mapping, null, Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), HostPlatform.Windows).Command?.Name);

        // Where Right is the stroke button, "Right + wheel up" is "stroke + wheel up": the first in list order fires (A7 cannot know the machine).
        Assert.True(CommandResolver.Resolve(mapping, null, Tick(HeldButtons.Stroke, MouseButton.Right), HostPlatform.Windows).Fires);
    }

    [Fact]
    public void AClickTrigger_FiresForAClickWithItsKeys_AndNothingElseDoes()
    {
        var mapping = Document(NewGlobal(NewCommand("Alt click", Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.Alt)), NewStep("x"))));

        Assert.Equal("Alt click", CommandResolver.Resolve(mapping, null, new PressedTrigger(Trigger.Click, Press(before: KeyModifiers.Alt)), HostPlatform.Windows).Command?.Name);
        Assert.False(CommandResolver.Resolve(mapping, null, new PressedTrigger(Trigger.Click, Press(before: KeyModifiers.Shift)), HostPlatform.Windows).Fires);
        Assert.Equal(Trigger.None, Trigger.ForClick(TriggerHold.Default));
    }

    [Fact]
    public void A7_ACombinationBesideThePlainTriggerIsFine_OverlappingModesAreNot()
    {
        var plain = NewCommand("Plain", Up);
        var shiftBefore = NewCommand("Shift before", Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Shift, capture: HoldCapture.Before)));
        var shiftAfter = NewCommand("Shift after", Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Shift, capture: HoldCapture.After)));
        var shiftEither = NewCommand("Shift either", Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Shift)));

        MappingRules.ValidDocument(Document(NewGlobal(plain, shiftBefore, shiftAfter)));
        var clash = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(plain, shiftBefore, shiftEither))));
        Assert.Contains("already uses Shift + this gesture", clash.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWheelTriggerHoldingNoButton_IsRefused()
    {
        var holdsNothing = NewCommand("Nothing held", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.None, KeyModifiers.Shift)));

        var refused = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(holdsNothing))));
        Assert.Contains("holds no button", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalising_AddsTheStrokeButtonToAGesture_AndDropsTheModeWhenNothingIsHeld()
    {
        Assert.Equal(TriggerHold.Default, Trigger.ForGesture(Up, new TriggerHold(HeldButtons.None, Capture: HoldCapture.After)).Hold);
        Assert.Equal(HeldButtons.Stroke | HeldButtons.Left, Trigger.ForGesture(Up, new TriggerHold(HeldButtons.Left)).Hold.Buttons);
        Assert.Equal(new TriggerHold(HeldButtons.Right), Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right, Capture: HoldCapture.Before)).Hold);
    }

    [Theory]
    [InlineData(HostPlatform.Windows, HostPlatform.MacOS, KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Meta | KeyModifiers.Shift)]
    [InlineData(HostPlatform.MacOS, HostPlatform.Windows, KeyModifiers.Meta | KeyModifiers.Alt, KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData(HostPlatform.MacOS, HostPlatform.Windows, KeyModifiers.Control, KeyModifiers.Control)]
    public void KeysConvertLikeAHotkeys(HostPlatform from, HostPlatform to, KeyModifiers keys, KeyModifiers expected)
    {
        var converted = TriggerConversion.Convert(Trigger.ForGesture(Up, TriggerHold.WithStroke(keys)), from, to);

        Assert.Equal(expected, converted.Trigger!.Hold.Keys);
        Assert.Equal(keys != expected, converted.IsConverted);
    }

    [Theory]
    [InlineData(HostPlatform.Windows, KeyModifiers.Meta, "the Windows key has no Mac counterpart")]
    [InlineData(HostPlatform.MacOS, KeyModifiers.Meta | KeyModifiers.Control, "Cmd and Ctrl together have no Windows counterpart")]
    public void KeysWithNoCounterpart_LeaveTheOtherPlatformUnbound(HostPlatform from, KeyModifiers keys, string reason)
    {
        var to = from == HostPlatform.Windows ? HostPlatform.MacOS : HostPlatform.Windows;
        var conversion = TriggerConversion.Convert(Trigger.ForWheel(WheelDirection.Up, TriggerHold.WithStroke(keys)), from, to);

        Assert.Null(conversion.Trigger);
        Assert.Contains(reason, conversion.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandAuthoredOnWindows_RunsWithCmdOnAMac_AndATriggerEditThereMakesItsOwn()
    {
        var ctrlUp = Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Control));
        var command = NewCommand("Copy", ctrlUp, NewStep("copy"));
        Assert.Equal(KeyModifiers.Meta, command.TriggerFor(HostPlatform.MacOS).Hold.Keys);
        Assert.Equal(ctrlUp, command.TriggerFor(HostPlatform.Windows));

        var edited = command.WithTriggerFor(HostPlatform.MacOS, Trigger.ForGesture(Down), DateTimeOffset.UnixEpoch);

        Assert.Equal(ctrlUp, edited.Trigger);
        Assert.Equal(Trigger.ForGesture(Down), edited.TriggerFor(HostPlatform.MacOS));
        Assert.True(edited.HasOwnTriggerOn(HostPlatform.MacOS));
        Assert.Equal(command.ConvertedFor(HostPlatform.MacOS), edited.OwnVersion!.Steps);
        Assert.True(edited.UsesGesture(Down));
        Assert.Equal(ctrlUp, edited.WithoutOwnVersion().TriggerFor(HostPlatform.Windows));
        Assert.Equal(Trigger.None, command.WithTriggerFor(HostPlatform.Windows, Trigger.None, DateTimeOffset.UnixEpoch).Trigger);
        Assert.Null(command.WithTriggerFor(HostPlatform.Windows, Trigger.None, DateTimeOffset.UnixEpoch).OwnVersion);
    }

    [Fact]
    public void ReplacingAGesture_KeepsWhatTheTriggerHolds_InTheOriginalAndTheOwnVersion()
    {
        var shiftUp = Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Shift));
        var command = NewCommand("Copy", shiftUp, NewStep("copy")).WithTriggerFor(HostPlatform.MacOS, shiftUp, DateTimeOffset.UnixEpoch);

        var moved = command.WithGestureReplaced(Up, Down);
        var unbound = command.WithGestureReplaced(Up, null);

        Assert.Equal(Trigger.ForGesture(Down, TriggerHold.WithStroke(KeyModifiers.Shift)), moved.Trigger);
        Assert.Equal(Trigger.ForGesture(Down, TriggerHold.WithStroke(KeyModifiers.Shift)), moved.OwnVersion!.Trigger);
        Assert.Equal(Trigger.None, unbound.Trigger);
        Assert.Equal(Trigger.None, unbound.OwnVersion!.Trigger);
    }
}
