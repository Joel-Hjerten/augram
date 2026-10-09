using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Scroll;
using Augram.Core.Steps.WindowOp;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// StrokesPlus.net sample scripts that do one thing an Augram step does, kept here as code (comments dropped) with the
/// step each becomes. A script matches a sample when its tokens are the sample's (<see cref="ScriptTokens"/>): layout,
/// comments and semicolons may differ, the code may not, so a sample someone edited (another margin, two notches) stays
/// a placeholder rather than being guessed at. Recognised by code only, never by the command's name.
/// <list type="bullet">
/// <item>The "Window Snap to Left / Right (half screen size)" samples, margin 0: restore, then the left or right half of
/// the window's screen's working area → <see cref="WindowOperation.SnapLeftHalf"/> / <see cref="WindowOperation.SnapRightHalf"/>.</item>
/// <item>The forum's "Ctrl + mouse wheel" zoom: <c>WM_MOUSEWHEEL</c> with <c>MK_CONTROL</c> and one standard tick (±120)
/// posted to the window under <c>action.Start</c> → Scroll up or down one notch with Ctrl held, at the gesture start.</item>
/// </list>
/// </summary>
internal static class SampleScripts
{
    private const string SnapLeftHalf = """
        var win = action.Window;
        var screen = win.Screen.WorkingArea;
        var margin = 0;
        var halfWidth = Math.floor(screen.Width / 2);
        win.Restore();
        var rect = win.Rectangle;
        rect.X = screen.X + margin;
        rect.Y = screen.Y + margin;
        rect.Width = halfWidth - margin*1.5;
        rect.Height = screen.Height - margin*2;
        win.Rectangle = rect;
        """;

    private const string SnapRightHalf = """
        var win = action.Window;
        var screen = win.Screen.WorkingArea;
        var margin = 0;
        var halfWidth = Math.floor(screen.Width / 2);
        win.Restore();
        var rect = win.Rectangle;
        rect.X = screen.X + halfWidth + margin*0.5;
        rect.Y = screen.Y + margin;
        rect.Width = halfWidth - margin*1.5;
        rect.Height = screen.Height - margin*2;
        win.Rectangle = rect;
        """;

    // {0} is WHEEL_POS (up) or WHEEL_NEG (down); the sample declares both.
    private const string ControlWheel = """
        var WM_MOUSEWHEEL = 0x20A;
        var WHEEL_POS = 0x00780000;
        var WHEEL_NEG = 0xff880000;
        var MK_CONTROL = 0x08;
        sp.WindowFromPoint(action.Start, false)
            .PostMessage(WM_MOUSEWHEEL,
                         new System.IntPtr({0}+MK_CONTROL),
                         new System.IntPtr((action.Start.Y << 16) + action.Start.X));
        """;

    private static readonly (IReadOnlyList<string> Tokens, IStep Step)[] Samples =
    [
        (Tokens(SnapLeftHalf), new WindowOpStep(WindowOperation.SnapLeftHalf)),
        (Tokens(SnapRightHalf), new WindowOpStep(WindowOperation.SnapRightHalf)),
        (Tokens(ControlWheel.Replace("{0}", "WHEEL_POS", StringComparison.Ordinal)), new ScrollStep(ScrollDirection.Up, 1, KeyModifiers.Control)),
        (Tokens(ControlWheel.Replace("{0}", "WHEEL_NEG", StringComparison.Ordinal)), new ScrollStep(ScrollDirection.Down, 1, KeyModifiers.Control)),
    ];

    /// <summary>The step of the sample whose code <paramref name="tokens"/> is; null when it is none of them.</summary>
    public static IStep? Match(IReadOnlyList<string> tokens)
    {
        foreach (var (sample, step) in Samples)
        {
            if (ScriptTokens.Same(tokens, sample))
            {
                return step;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> Tokens(string code)
        => ScriptTokens.Read(code) ?? throw new InvalidOperationException("A sample script does not read.");
}
