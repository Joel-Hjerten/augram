using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.ClearClipboard;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.Scroll;
using Augram.Core.Steps.WindowOp;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// The script shapes a script-only action can be turned into steps from (reference config §4), recognised by code and
/// never by a command's name. The scripts are synthetic: SP.net's own samples, laid out and commented differently from
/// any real config, plus variations that must stay placeholders.
/// </summary>
public sealed class ScriptMappingTests
{
    private const string SnapRight = """
        /* Snap the window to the right half */
        var win = action.Window;
        var screen = win.Screen.WorkingArea;
        var margin = 0; // px
        var halfWidth = Math.floor(screen.Width / 2);

        win.Restore(); // un-maximize first
        var rect = win.Rectangle;
        rect.X = screen.X + halfWidth + margin * 0.5;
        rect.Y = screen.Y + margin;
        rect.Width = halfWidth - margin * 1.5;
        rect.Height = screen.Height - margin * 2;
        win.Rectangle = rect
        """;

    private const string ControlWheel = """
        // Ctrl + wheel, posted to the window under the start point
        var WM_MOUSEWHEEL = 0x20A;
        var WHEEL_POS = 0x00780000; // +120
        var WHEEL_NEG = 0xff880000; // -120
        var MK_CONTROL = 0x08;
        sp.WindowFromPoint(action.Start, false).PostMessage(WM_MOUSEWHEEL, new System.IntPtr(TICK + MK_CONTROL), new System.IntPtr((action.Start.Y << 16) + action.Start.X));
        """;

    [Fact]
    public void ASingleRunProgramCallStillBecomesItsStep()
    {
        var steps = ScriptMapping.TryMap("sp.RunProgram('explorer.exe', '', 'open', 'normal', true, false, false);");

        Assert.Equal<IStep>([new RunStep("explorer.exe")], steps);
    }

    [Theory]
    [InlineData("//Do nothing here, the purpose of this is to ignore the default action\r\n//on the desktop")]
    [InlineData("/* nothing */\n\n   // still nothing\n")]
    [InlineData("   \r\n\t")]
    public void OnlyCommentsAndBlanksAreNoSteps(string script)
    {
        Assert.Empty(ScriptMapping.TryMap(script)!);
    }

    [Fact]
    public void AnUnclosedCommentIsNotRead()
    {
        Assert.Null(ScriptMapping.TryMap("/* nothing here"));
    }

    [Theory]
    [InlineData("sp.SendKeys(\"^{ADD}\");", KeyCode.NumPadAdd)]
    [InlineData("//Zoom out\r\nsp.SendKeys('^{SUBTRACT}')\r\n//sp.SendKeys(\"^{ADD}\");", KeyCode.NumPadSubtract)]
    public void OneSendKeysCallBecomesItsKeys(string script, KeyCode key)
    {
        var steps = ScriptMapping.TryMap(script);

        Assert.Equal<IStep>([new HotkeyStep(KeyModifiers.Control, key)], steps);
        Assert.Equal("Ctrl+Num " + (key == KeyCode.NumPadAdd ? "+" : "-"), Assert.Single(steps!).Summary);
    }

    [Theory]
    [InlineData("sp.SendKeys(\"{BREAK}\");")]
    [InlineData("sp.SendKeys(\"\");")]
    [InlineData("sp.SendKeys(\"^c\"); sp.Sleep(50);")]
    [InlineData("sp.SendKeys(\"^\" + \"c\");")]
    [InlineData("sp.SendKeys(keys);")]
    [InlineData("var keys = \"^c\"; sp.SendKeys(keys);")]
    public void SendKeysThatDoesNotMapCleanlyStaysAPlaceholder(string script)
    {
        Assert.Null(ScriptMapping.TryMap(script));
    }

    [Theory]
    [InlineData("clip.Clear();")]
    [InlineData("// Clear the clipboard\r\nclip.Clear()")]
    [InlineData("clip . Clear ( ) ;")]
    public void ClipClearAloneBecomesClearClipboard(string script)
    {
        Assert.Equal<IStep>([new ClearClipboardStep()], ScriptMapping.TryMap(script));
    }

    [Theory]
    [InlineData("clip.Clear(); sp.Beep();")]
    [InlineData("clip.clear();")]
    [InlineData("clip.SetText(\"\");")]
    public void AnythingMoreOrElseThanClipClearStaysAPlaceholder(string script)
    {
        Assert.Null(ScriptMapping.TryMap(script));
    }

    [Fact]
    public void TheHalfScreenSnapSampleBecomesTheSnap_LeftOrRight()
    {
        var left = SnapRight.Replace("screen.X + halfWidth + margin * 0.5", "screen.X + margin", StringComparison.Ordinal);

        Assert.Equal<IStep>([new WindowOpStep(WindowOperation.SnapRightHalf)], ScriptMapping.TryMap(SnapRight));
        Assert.Equal<IStep>([new WindowOpStep(WindowOperation.SnapLeftHalf)], ScriptMapping.TryMap(left));
    }

    [Theory]
    [InlineData("var margin = 0;", "var margin = 10;")]
    [InlineData("Math.floor(screen.Width / 2)", "Math.floor(screen.Width / 3)")]
    [InlineData("win.Rectangle = rect", "win.Rectangle = rect; win.Maximize();")]
    public void AnEditedSnapSampleStaysAPlaceholder(string sample, string edited)
    {
        Assert.Null(ScriptMapping.TryMap(SnapRight.Replace(sample, edited, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("WHEEL_POS", ScrollDirection.Up)]
    [InlineData("WHEEL_NEG", ScrollDirection.Down)]
    public void TheCtrlWheelPostBecomesACtrlScrollOfOneNotch(string tick, ScrollDirection direction)
    {
        var steps = ScriptMapping.TryMap(ControlWheel.Replace("TICK", tick, StringComparison.Ordinal));

        Assert.Equal<IStep>([new ScrollStep(direction, 1, KeyModifiers.Control)], steps);
    }

    [Theory]
    [InlineData("TICK", "WHEEL_POS * 2")]
    [InlineData("MK_CONTROL = 0x08", "MK_CONTROL = 0x04")]
    [InlineData("var WHEEL_NEG = 0xff880000; // -120", "")]
    public void AnEditedWheelPostStaysAPlaceholder(string sample, string edited)
    {
        var script = ControlWheel.Replace(sample, edited, StringComparison.Ordinal).Replace("TICK", "WHEEL_POS", StringComparison.Ordinal);

        Assert.Null(ScriptMapping.TryMap(script));
    }

    [Theory]
    [InlineData("sp.MessageBox(\"hi\", \"title\");")]
    [InlineData("var x = \"unclosed;")]
    [InlineData("sp.Beep();")]
    public void AnyOtherScriptStaysAPlaceholder(string script)
    {
        Assert.Null(ScriptMapping.TryMap(script));
    }
}
