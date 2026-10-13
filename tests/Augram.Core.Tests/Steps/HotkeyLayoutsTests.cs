using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>
/// A hotkey per keyboard layout (plan 0011): the step's <c>Layouts</c> map and <c>ForLayout</c>, the " · per layout" summary,
/// the <c>layouts</c> parameter's round trip and refusals, the conversion dropping the map, and the executor picking by the
/// active layout. The main shortcut alone is <see cref="HotkeyStepTests"/>.
/// </summary>
public sealed class HotkeyLayoutsTests
{
    private const string SwedishId = "com.apple.keylayout.Swedish-Pro";
    private const string AbcId = "com.apple.keylayout.ABC";

    private static readonly HotkeyStepType Type = HotkeyStepType.Instance;
    private static readonly HotkeyBinding CmdOpenBracket = new(KeyModifiers.Meta, KeyCode.OpenBracket);
    private static readonly HotkeyBinding CmdOptDigit8 = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8);

    /// <summary>Chrome's Back: Cmd+[ under ABC, authored as Cmd+Opt+8 under Swedish.</summary>
    private static HotkeyStep Back(params (string Id, HotkeyBinding Binding)[] layouts)
        => new(KeyModifiers.Meta, KeyCode.OpenBracket) { Layouts = layouts.ToDictionary(pair => pair.Id, pair => pair.Binding, StringComparer.Ordinal) };

    [Fact]
    public void ForLayoutPicksTheLayoutsShortcut_AndFallsBackToTheMainOne()
    {
        var step = Back((SwedishId, CmdOptDigit8));

        Assert.True(step.HasLayouts);
        Assert.Equal(CmdOptDigit8, step.ForLayout(SwedishId));
        Assert.Equal(CmdOpenBracket, step.ForLayout(AbcId));
        Assert.Equal(CmdOpenBracket, step.ForLayout(null));
        Assert.Equal(CmdOpenBracket, step.Main);
        Assert.False(HotkeyStep.Unset.HasLayouts);
        Assert.Equal(new HotkeyBinding(KeyModifiers.None, KeyCode.None), HotkeyStep.Unset.ForLayout(SwedishId));
    }

    [Fact]
    public void SummaryAddsPerLayout_OnlyWhenTheStepHasLayoutShortcuts()
    {
        var step = Back((SwedishId, CmdOptDigit8));

        Assert.Equal("Win+[ · per layout", step.Summary);
        Assert.Equal("Cmd+[ · per layout", step.SummaryOn(HostPlatform.MacOS));
        Assert.Equal("Win+[", new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket).Summary);
        Assert.Equal("Hotkey (no key set) · per layout", (HotkeyStep.Unset with { Layouts = step.Layouts }).Summary);
    }

    [Fact]
    public void TwoStepsWithTheSameShortcutsAreEqual_WhateverMapHoldsThem()
    {
        var a = Back((SwedishId, CmdOptDigit8), (AbcId, CmdOpenBracket));
        var b = new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket)
        {
            Layouts = new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal) { [AbcId] = CmdOpenBracket, [SwedishId] = CmdOptDigit8 },
        };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Back((SwedishId, CmdOptDigit8)));
        Assert.NotEqual(a, Back((SwedishId, CmdOptDigit8), (AbcId, CmdOptDigit8)));
        Assert.NotEqual(a, new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket));
    }

    [Fact]
    public void LayoutsRoundTripByteStable_InIdOrder_AfterTheMainShortcut()
    {
        var step = Back((SwedishId, CmdOptDigit8), (AbcId, new HotkeyBinding(KeyModifiers.Meta | KeyModifiers.Shift, KeyCode.Z, KeyModifiers.Shift)));

        var written = Type.Write(step);

        Assert.Equal(
            """{"modifiers":"Meta","key":"OpenBracket","layouts":{"com.apple.keylayout.ABC":{"modifiers":"Shift, Meta","rightHand":"Shift","key":"Z"},"com.apple.keylayout.Swedish-Pro":{"modifiers":"Alt, Meta","key":"Digit8"}}}""",
            written.ToJsonString());
        var reread = Type.Read(written);
        Assert.Equal(step, reread);
        Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
    }

    [Fact]
    public void LayoutsIsOmittedWhenEmpty_SoEveryExistingStepsJsonIsUnchanged()
    {
        var plain = new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T);

        Assert.Equal("""{"modifiers":"Control, Shift","key":"T"}""", Type.Write(plain).ToJsonString());
        Assert.False(Type.Write(plain with { Layouts = new Dictionary<string, HotkeyBinding>() }).ContainsKey(HotkeyStepType.LayoutsMember));
        Assert.Same(HotkeyStep.NoLayouts, Assert.IsType<HotkeyStep>(Type.Read(StepJson.Object("""{ "modifiers": "Control", "key": "T", "layouts": null }"""))).Layouts);
        Assert.Equal(plain, Type.Read(StepJson.Object("""{ "modifiers": "Control, Shift", "key": "T" }""")));
    }

    [Fact]
    public void ReadNormalisesEachLayoutShortcut_AndDropsOneWithoutAKey()
    {
        var step = Assert.IsType<HotkeyStep>(Type.Read(StepJson.Object(
            $$"""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "{{SwedishId}}": { "modifiers": "alt, META", "rightHand": "Shift, Alt", "key": "digit8" }, "{{AbcId}}": { "modifiers": "Meta" } } }""")));

        Assert.Equal(Back((SwedishId, new HotkeyBinding(KeyModifiers.Alt | KeyModifiers.Meta, KeyCode.Digit8, KeyModifiers.Alt))), step);
    }

    [Fact]
    public void NormalizedCutsEachLayoutsRightHandAndDropsUnsetEntries_SameInstanceWhenNothingToDo()
    {
        var clean = Back((SwedishId, CmdOptDigit8));
        var stray = Back((SwedishId, CmdOptDigit8 with { RightHand = KeyModifiers.Control | KeyModifiers.Alt }), (AbcId, new HotkeyBinding(KeyModifiers.Meta, KeyCode.None)));

        Assert.Same(clean, clean.Normalized());
        Assert.Equal(Back((SwedishId, CmdOptDigit8 with { RightHand = KeyModifiers.Alt })), stray.Normalized());
        Assert.Equal("RAlt+Win+8", stray.Normalized().ForLayout(SwedishId).Summary);
    }

    [Theory]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": [] }""", "'layouts' must be an object of layout id to hotkey.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": "ABC" }""", "'layouts' must be an object of layout id to hotkey.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "": { "key": "A" } } }""", "'layouts' holds an empty layout id.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "  ": { "key": "A" } } }""", "'layouts' holds an empty layout id.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "ABC": "Cmd+[" } }""", "'layouts' entry 'ABC' must be an object with modifiers, rightHand and key.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "ABC": null } }""", "'layouts' entry 'ABC' must be an object with modifiers, rightHand and key.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "ABC": { "modifiers": "Cmd", "key": "A" } } }""", "'layouts' entry 'ABC': 'modifiers' must list Control, Alt, Shift, Meta separated by commas, or None; got 'Cmd'.")]
    [InlineData("""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "ABC": { "modifiers": "Meta", "key": 20 } } }""", "'layouts' entry 'ABC': 'key' must be a string.")]
    public void ABadLayoutsMemberNamesIt(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void ABadKeyNameInsideALayoutNamesTheLayoutsEntry()
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object($$"""{ "modifiers": "Meta", "key": "OpenBracket", "layouts": { "{{AbcId}}": { "modifiers": "Meta", "key": "Hyper" } } }""")));

        Assert.StartsWith($"'layouts' entry '{AbcId}': 'key' must be one of None, A, B,", ex.Message, StringComparison.Ordinal);
        Assert.IsType<StepFormatException>(ex.InnerException);
    }

    [Fact]
    public void ConvertDropsTheLayoutShortcuts_AndConvertsTheMainOneAsBefore()
    {
        var step = Back((SwedishId, CmdOptDigit8)) with { Modifiers = KeyModifiers.Control };

        var converted = Type.Convert(step, HostPlatform.Windows, HostPlatform.MacOS);
        var same = Type.Convert(new HotkeyStep(KeyModifiers.None, KeyCode.F5) with { Layouts = step.Layouts }, HostPlatform.MacOS, HostPlatform.Windows);
        var unset = Type.Convert(HotkeyStep.Unset with { Layouts = step.Layouts }, HostPlatform.MacOS, HostPlatform.Windows);
        var home = Type.Convert(step, HostPlatform.Windows, HostPlatform.Windows);

        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket), Assert.IsType<HotkeyStep>(converted.Step));
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.F5), Assert.IsType<HotkeyStep>(same.Step));
        Assert.Equal(HotkeyStep.Unset, Assert.IsType<HotkeyStep>(unset.Step));
        Assert.Same(step, home.Step);
        Assert.Same(step, Type.Convert(step, HostPlatform.Windows, HostPlatform.Windows).Step);
    }

    [Fact]
    public void ExecuteSendsTheActiveLayoutsShortcut_AndSaysSoInTheLog()
    {
        var input = new FakeInputSimulator();
        var log = new ListEventLog();
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc);

        var result = Type.Execute(Back((SwedishId, CmdOptDigit8)), StepContexts.Create(input: input, log: log, layouts: layouts));

        Assert.Equal(StepResult.Done, result);
        Assert.Equal(["hotkey Alt, Meta+Digit8"], input.All);
        Assert.Equal(1, layouts.ActiveReads);
        var line = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Debug, "steps", "Hotkey"), (line.Level, line.Source, line.Message));
        Assert.Contains(new LogProperty("keys", "Alt+Win+8"), line.Properties!);
        Assert.Contains(new LogProperty("layout", "Swedish"), line.Properties!);
        Assert.Contains(new LogProperty("shortcut", "layout shortcut"), line.Properties!);
    }

    [Fact]
    public void ExecuteSendsTheMainShortcut_WhenTheMapLacksTheActiveLayout()
    {
        var input = new FakeInputSimulator();
        var log = new ListEventLog();
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Abc, FakeKeyboardLayouts.Swedish);

        Type.Execute(Back((SwedishId, CmdOptDigit8)), StepContexts.Create(input: input, log: log, layouts: layouts));

        Assert.Equal(["hotkey Meta+OpenBracket"], input.All);
        Assert.Contains(new LogProperty("keys", "Win+["), Assert.Single(log.Events).Properties!);
        Assert.Contains(new LogProperty("layout", "ABC"), log.Last.Properties!);
        Assert.Contains(new LogProperty("shortcut", "fallback"), log.Last.Properties!);
    }

    [Fact]
    public void ExecuteSendsTheMainShortcut_WhenThePlatformCannotSayWhichLayoutIsActive()
    {
        var input = new FakeInputSimulator();
        var log = new ListEventLog();
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish) { Active = null };

        Type.Execute(Back((SwedishId, CmdOptDigit8)), StepContexts.Create(input: input, log: log, layouts: layouts));
        Type.Execute(Back((SwedishId, CmdOptDigit8)), StepContexts.Create(input: input));

        Assert.Equal(["hotkey Meta+OpenBracket", "hotkey Meta+OpenBracket"], input.All);
        Assert.Contains(new LogProperty("layout", "unknown"), log.Last.Properties!);
        Assert.Contains(new LogProperty("shortcut", "fallback"), log.Last.Properties!);
    }

    [Fact]
    public void AStepWithoutLayoutShortcutsNeverAsksWhichLayoutIsActive_AndItsLogLineSaysNothingOfLayouts()
    {
        var log = new ListEventLog();
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish);

        Type.Execute(new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket), StepContexts.Create(log: log, layouts: layouts));

        Assert.Equal(0, layouts.ActiveReads);
        var line = Assert.Single(log.Events);
        Assert.DoesNotContain(line.Properties!, property => property.Key is "layout" or "shortcut");
    }

    [Fact]
    public void ALayoutShortcutTheSimulatorRefusesFailsWithItsOwnKeys()
    {
        var input = new FakeInputSimulator { OtherResult = SimulationResult.Unsupported };
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish);

        var result = Type.Execute(Back((SwedishId, CmdOptDigit8)), StepContexts.Create(input: input, layouts: layouts));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("Alt+Win+8: Unsupported", result.Reason);
    }
}
