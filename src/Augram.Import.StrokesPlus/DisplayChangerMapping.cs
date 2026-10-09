using System.Globalization;
using System.Text;
using Augram.Core.Abstractions;
using Augram.Core.Steps.DisplayMode;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns a 12noon Display Changer command line (the program <c>dc64cmd.exe</c> or <c>dccmd.exe</c> plus its argument
/// string, as an SP.net <c>sp.RunProgram</c> script runs it) into a <see cref="DisplayModeStep"/>, or null when it is
/// not one or asks for something the step cannot express (learnings 0002 §1, §4). Understood: <c>-width=N</c> and
/// <c>-height=N</c> (both or neither), <c>-refresh=N</c> or <c>-refresh=max</c> (the step's "highest available"), and
/// <c>-quiet</c>. <c>-refresh=N</c> is Windows' whole-hertz rate, so it goes through <see cref="RefreshRate.FromLegacyHertz"/>
/// (23 → 23.976, 120 → 120). Anything else (<c>-depth</c>, <c>-monitor</c>, <c>-force</c>, <c>-test</c>, a program to run
/// afterwards) gives null, and the command stays a Run step. The target is the step's default, the display under the
/// gesture: Display Changer changed the primary display, which is the same display on a one-display PC. Pure; reached
/// through <see cref="ProgramCallMapping"/>.
/// </summary>
public static class DisplayChangerMapping
{
    private static readonly string[] ProgramNames = ["dc64cmd.exe", "dccmd.exe"];

    /// <summary>True when <paramref name="fileName"/> (a path with either separator, quoted or not) names a Display Changer command-line program.</summary>
    public static bool IsDisplayChanger(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var path = fileName.Trim().Trim('"');
        var name = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
        return ProgramNames.Any(program => string.Equals(program, name, StringComparison.OrdinalIgnoreCase));
    }

    public static DisplayModeStep? FromInvocation(string fileName, string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!IsDisplayChanger(fileName))
        {
            return null;
        }

        int? width = null;
        int? height = null;
        RefreshRate? refresh = null;
        var highest = false;
        foreach (var token in Tokens(arguments))
        {
            if (token.Length < 2 || token[0] is not ('-' or '/'))
            {
                return null;
            }

            var body = token[1..];
            var equals = body.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? body : body[..equals];
            var value = equals < 0 ? null : body[(equals + 1)..];
            if (Is(name, "quiet") && value is null)
            {
                continue;
            }

            if (Is(name, "width") && width is null && Side(value) is { } w)
            {
                width = w;
            }
            else if (Is(name, "height") && height is null && Side(value) is { } h)
            {
                height = h;
            }
            else if (Is(name, "refresh") && refresh is null && !highest && Is(value ?? string.Empty, "max"))
            {
                highest = true;
            }
            else if (Is(name, "refresh") && refresh is null && !highest && Rate(value) is { } rate)
            {
                refresh = rate;
            }
            else
            {
                return null;
            }
        }

        if (width.HasValue != height.HasValue || (width is null && refresh is null && !highest))
        {
            return null;
        }

        return new DisplayModeStep(width is { } wide && height is { } high ? new DisplayResolution(wide, high) : null, refresh, HighestRefresh: highest);
    }

    private static bool Is(string name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    private static int? Side(string? value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var side) && side >= DisplayResolution.MinSide && side <= DisplayResolution.MaxSide
            ? side
            : null;

    private static RefreshRate? Rate(string? value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var hertz) && RefreshRate.FromLegacyHertz(hertz) is { IsKnown: true } rate
            ? rate
            : null;

    /// <summary>Whitespace-separated tokens; a double-quoted stretch keeps its spaces (a quoted monitor name or program path).</summary>
    private static List<string> Tokens(string arguments)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in arguments)
        {
            if (c == '"')
            {
                quoted = !quoted;
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
    }
}
