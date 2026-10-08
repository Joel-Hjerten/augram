using Augram.Core.Abstractions;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.Run;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns SP.net's ways of starting a program into <see cref="RunStep"/>s (plan 0001 §C1), pure and stateless like
/// <see cref="HotkeyMapping"/>. The <c>Run</c> step carries one parameter, <c>command</c>: a whole command line
/// (<c>explorer</c>, <c>ms-settings:display</c>, <c>"C:\path with spaces\x.exe" args</c>) that <see cref="SplitCommand"/>
/// cuts into file and arguments. A <c>sp.RunProgram(…)</c> script, once <see cref="RunProgramScript"/> recognised it, maps
/// through <see cref="FromRunProgram"/>: verb <c>runas</c> is elevated, style <c>hidden</c> is hidden; <c>noWindow</c>,
/// <c>waitForExit</c>, other styles (minimized, maximized) and other verbs (edit, print) have no Run step equivalent
/// and are dropped (check <see cref="RunProgramCall.HasPlainVerb"/> first to keep those as placeholders instead).
/// Not wired into the step reader yet.
/// </summary>
public static class RunMapping
{
    /// <summary>The <c>Run</c> step's one parameter.</summary>
    public const string CommandParameter = "command";

    // A prefix of an unquoted command line that ends in one of these is the program (C:\Program Files\x\x.exe -flag).
    private static readonly string[] ProgramExtensions = [".exe", ".com", ".bat", ".cmd", ".lnk", ".msc", ".cpl"];

    /// <summary>The <c>Run</c> step's parameters as a step; null when <c>command</c> is missing or blank.</summary>
    public static RunStep? FromRun(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!parameters.TryGetValue(CommandParameter, out var command) || string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var (file, arguments) = SplitCommand(command);
        return new RunStep(file, arguments);
    }

    /// <summary>A recognised <c>sp.RunProgram</c> call as a step: file and arguments as written, elevated for <c>runas</c>, hidden for style <c>hidden</c>.</summary>
    public static RunStep FromRunProgram(RunProgramCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return new RunStep(call.FileName, call.Arguments, string.Empty, call.IsElevated, call.IsHidden);
    }

    /// <summary>A placeholder an earlier import stored for <c>Run</c>, as the real step; null for any other method or no command.</summary>
    public static RunStep? TryUpgrade(ImportedStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.SourceMethod == StrokesPlusJson.Method.Run ? FromRun(step.Parameters) : null;
    }

    /// <summary>
    /// A command line cut into file and arguments, the way a person reads it: a quoted first part is the file; a URI is
    /// all file; otherwise the shortest run of words ending in a program extension (<c>.exe</c>, <c>.bat</c>, …) is the
    /// file, so an unquoted path with spaces keeps together; failing that a rooted path (<c>C:\…</c>, <c>\\server\…</c>)
    /// is all file (a document or folder with spaces), and anything else splits at the first blank.
    /// </summary>
    public static (string File, string Arguments) SplitCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var text = command.Trim();
        if (text.StartsWith('"'))
        {
            var close = text.IndexOf('"', 1);
            return close < 0 ? (text.Trim('"').Trim(), string.Empty) : (text[1..close].Trim(), text[(close + 1)..].Trim());
        }

        if (ProcessLaunch.SchemeOf(text) is not null)
        {
            return (text, string.Empty);
        }

        for (var blank = text.IndexOf(' '); ; blank = text.IndexOf(' ', blank + 1))
        {
            var end = blank < 0 ? text.Length : blank;
            if (EndsWithProgramExtension(text.AsSpan(0, end)))
            {
                return (text[..end], text[end..].Trim());
            }

            if (blank < 0)
            {
                break;
            }
        }

        var first = text.IndexOfAny([' ', '\t']);
        return first < 0 || IsRooted(text) ? (text, string.Empty) : (text[..first], text[first..].Trim());
    }

    private static bool EndsWithProgramExtension(ReadOnlySpan<char> file)
    {
        foreach (var extension in ProgramExtensions)
        {
            if (file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A drive path (<c>C:\</c>, <c>C:/</c>) or a UNC path (<c>\\server</c>).</summary>
    private static bool IsRooted(string text)
        => text.StartsWith(@"\\", StringComparison.Ordinal)
            || (text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[2] is '\\' or '/');
}
