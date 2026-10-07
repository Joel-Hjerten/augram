using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// SP.net <c>SendHotKey</c> / <c>SendVKey</c> parameters → <see cref="HotkeyStep"/>. Every dictionary is
/// synthetic but in the real shape: <see cref="MethodParameterReader"/> keeps the <c>hotkey</c> object as
/// compact JSON text with all eight side flags and a numeric virtual-key <c>Key</c>.
/// </summary>
public sealed class HotkeyMappingTests
{
    private static Dictionary<string, string> HotKey(string json) => new(StringComparer.Ordinal) { ["hotkey"] = json };

    private static string Full(bool lControl = false, bool rControl = false, bool lAlt = false, bool rAlt = false, bool lShift = false, bool rShift = false, bool lWin = false, bool rWin = false, int key = 0)
        => $$"""{"LControl":{{B(lControl)}},"RControl":{{B(rControl)}},"LAlt":{{B(lAlt)}},"RAlt":{{B(rAlt)}},"LShift":{{B(lShift)}},"RShift":{{B(rShift)}},"LWin":{{B(lWin)}},"RWin":{{B(rWin)}},"Key":{{key}}}""";

    private static string B(bool value) => value ? "true" : "false";

    [Fact]
    public void TheFullShapeMapsModifiersAndKey()
    {
        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T), HotkeyMapping.FromSendHotKey(HotKey(Full(lControl: true, lShift: true, key: 84))));
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.Enter), HotkeyMapping.FromSendHotKey(HotKey(Full(key: 13))));
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt | KeyModifiers.Meta, KeyCode.Left), HotkeyMapping.FromSendHotKey(HotKey(Full(lAlt: true, lWin: true, key: 37))));
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.Tab), HotkeyMapping.FromSendHotKey(HotKey("""{"LControl":true,"Key":9}""")));
    }

    [Fact]
    public void RightSideModifiersAreKept()
    {
        // Joel's own: RAlt+F9, RAlt+F10, RControl+RShift+P, RControl+0 (some apps misbehave with plain Alt).
        var rAltF9 = HotkeyMapping.FromSendHotKey(HotKey(Full(rAlt: true, key: 120)));
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), rAltF9);
        Assert.Equal("RAlt+F9", rAltF9!.Summary);
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F10, KeyModifiers.Alt), HotkeyMapping.FromSendHotKey(HotKey(Full(rAlt: true, key: 121))));
        Assert.Equal(
            new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.P, KeyModifiers.Control | KeyModifiers.Shift),
            HotkeyMapping.FromSendHotKey(HotKey(Full(rControl: true, rShift: true, key: 80))));
        var rControl0 = HotkeyMapping.FromSendHotKey(HotKey(Full(rControl: true, key: 48)));
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.Digit0, KeyModifiers.Control), rControl0);
        Assert.Equal("RCtrl+0", rControl0!.Summary);
        Assert.Equal(
            new HotkeyStep(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt),
            HotkeyMapping.FromSendHotKey(HotKey(Full(lControl: true, rAlt: true, key: 120))));
        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.D, KeyModifiers.Meta), HotkeyMapping.FromSendHotKey(HotKey("""{"RWin":true,"Key":68}""")));
    }

    [Fact]
    public void AModifierSetOnBothSidesIsThePlainOne()
    {
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9), HotkeyMapping.FromSendHotKey(HotKey(Full(lAlt: true, rAlt: true, key: 120))));
        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.D), HotkeyMapping.FromSendHotKey(HotKey(Full(rWin: true, lWin: true, key: 68))));
        Assert.Equal(
            new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.P, KeyModifiers.Shift),
            HotkeyMapping.FromSendHotKey(HotKey(Full(lControl: true, rControl: true, rShift: true, key: 80))));
        // A WinForms modifier bit names no side and counts as the left key, like the generic VK_CONTROL.
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.P), HotkeyMapping.FromSendHotKey(HotKey("""{"RControl":true,"Key":131152}""")));
    }

    [Fact]
    public void ToleratesStringValuesCaseAndWinFormsModifierBits()
    {
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.W), HotkeyMapping.FromSendHotKey(HotKey("""{"lcontrol":"True","key":"87"}""")));
        // Keys.Control | Keys.Shift | Keys.S, as System.Windows.Forms would write it.
        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.S), HotkeyMapping.FromSendHotKey(HotKey("""{"Key":196691}""")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"LControl":true}""")]
    [InlineData("""{"LControl":true,"Key":null}""")]
    [InlineData("""{"LControl":true,"Key":0}""")]
    [InlineData("""{"LControl":true,"Key":255}""")]
    [InlineData("""{"LControl":true,"Key":12.5}""")]
    public void UnreadableOrUnmappedGivesNull(string? json)
    {
        var parameters = json is null ? new Dictionary<string, string>(StringComparer.Ordinal) : HotKey(json);

        Assert.Null(HotkeyMapping.FromSendHotKey(parameters));
    }

    [Theory]
    [InlineData("9", KeyCode.Tab)]
    [InlineData("166", KeyCode.BrowserBack)]
    [InlineData("167", KeyCode.BrowserForward)]
    [InlineData("168", KeyCode.BrowserRefresh)]
    [InlineData("116.0", KeyCode.F5)]
    public void SendVKeyWithANonMediaKeyIsAHotkeyWithoutModifiers(string value, KeyCode expected)
    {
        var step = HotkeyMapping.FromSendVKey(new Dictionary<string, string> { ["virtualKey"] = value });

        Assert.Equal(new HotkeyStep(KeyModifiers.None, expected), step);
    }

    [Theory]
    [InlineData("173")]
    [InlineData("179")]
    [InlineData("255")]
    [InlineData("")]
    [InlineData("abc")]
    public void SendVKeyLeavesMediaKeysAndUnknownCodesAlone(string value)
    {
        Assert.Null(HotkeyMapping.FromSendVKey(new Dictionary<string, string> { ["virtualKey"] = value }));
        Assert.Null(HotkeyMapping.FromSendVKey(new Dictionary<string, string>()));
    }

    [Fact]
    public void TryUpgradeConvertsTheTwoKeyboardPlaceholdersOnly()
    {
        var hotkey = new ImportedStep("SendHotKey", "Send Hot Key", HotKey(Full(lControl: true, key: 87)));
        var rightHand = new ImportedStep("SendHotKey", "Send Hot Key", HotKey(Full(rAlt: true, key: 120)));
        var vkey = new ImportedStep("SendVKey", "Send Virtual Key", new Dictionary<string, string> { ["virtualKey"] = "9" });
        var broken = new ImportedStep("SendHotKey", "Send Hot Key", HotKey("{}"));
        var other = new ImportedStep("SendKeys", "Send Keys", new Dictionary<string, string> { ["hotkey"] = Full(lControl: true, key: 87) });

        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.W), HotkeyMapping.TryUpgrade(hotkey));
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), HotkeyMapping.TryUpgrade(rightHand));
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.Tab), HotkeyMapping.TryUpgrade(vkey));
        Assert.Null(HotkeyMapping.TryUpgrade(broken));
        Assert.Null(HotkeyMapping.TryUpgrade(other));
    }

    [Theory]
    [InlineData(0x41, KeyCode.A)]
    [InlineData(0x5A, KeyCode.Z)]
    [InlineData(0x30, KeyCode.Digit0)]
    [InlineData(0x39, KeyCode.Digit9)]
    [InlineData(0x60, KeyCode.NumPad0)]
    [InlineData(0x69, KeyCode.NumPad9)]
    [InlineData(0x70, KeyCode.F1)]
    [InlineData(0x87, KeyCode.F24)]
    [InlineData(0x21, KeyCode.PageUp)]
    [InlineData(0x22, KeyCode.PageDown)]
    [InlineData(0x2C, KeyCode.PrintScreen)]
    [InlineData(0x1B, KeyCode.Escape)]
    [InlineData(0x11, KeyCode.LeftControl)]
    [InlineData(0xA5, KeyCode.RightAlt)]
    [InlineData(0x5B, KeyCode.LeftMeta)]
    [InlineData(0xBE, KeyCode.Period)]
    [InlineData(0xBF, KeyCode.Slash)]
    [InlineData(0xDE, KeyCode.Quote)]
    [InlineData(0xB3, KeyCode.MediaPlay)]
    [InlineData(0x07, KeyCode.None)]
    [InlineData(-1, KeyCode.None)]
    public void VirtualKeyTable(int virtualKey, KeyCode expected)
    {
        Assert.Equal(expected, HotkeyMapping.FromVirtualKey(virtualKey));
    }

    [Fact]
    public void EveryKeyCodeButNumPadEnterIsReachableFromAVirtualKey()
    {
        var reachable = Enumerable.Range(0, 256).Select(HotkeyMapping.FromVirtualKey).ToHashSet();
        var missing = Enum.GetValues<KeyCode>().Where(key => key != KeyCode.None && !reachable.Contains(key)).ToList();

        // Numpad Enter is VK_RETURN plus the extended-key flag; SP.net's Key cannot say that.
        Assert.Equal([KeyCode.NumPadEnter], missing);
    }

    [Fact]
    public void MediaRangeIsTheReadersMediaKeys()
    {
        Assert.False(HotkeyMapping.IsMediaVirtualKey(172));
        Assert.True(HotkeyMapping.IsMediaVirtualKey(173));
        Assert.True(HotkeyMapping.IsMediaVirtualKey(179));
        Assert.False(HotkeyMapping.IsMediaVirtualKey(180));
    }
}
