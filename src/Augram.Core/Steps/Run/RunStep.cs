using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Run;

/// <summary>
/// Starts a program, document, folder or URI and goes on at once (A4: the command-line step is called Run; F5 "run a
/// command line", "run program with arguments, elevated/hidden flags"). <see cref="File"/> is what the user typed or
/// browsed to: a path, a bare name on the PATH or in App Paths (<c>explorer</c>), or a URI (<c>ms-settings:display</c>);
/// an empty one is the "no program set yet" a new step starts with, and running it skips. <see cref="Arguments"/> is one
/// command-line string; an empty <see cref="WorkingDirectory"/> is the launcher's default. <see cref="Elevated"/> runs
/// it as administrator (Windows UAC), <see cref="Hidden"/> without a window. Paths are platform-bound (F8):
/// <see cref="RunConversion"/> keeps only cross-platform links on the other platform.
/// </summary>
public sealed record RunStep(string File, string Arguments = "", string WorkingDirectory = "", bool Elevated = false, bool Hidden = false) : IStep
{
    public const string UnsetSummary = "Run (no program set)";

    /// <summary>No program: what the picker adds.</summary>
    public static RunStep Unset { get; } = new(string.Empty);

    public IStepType Type => RunStepType.Instance;

    public bool IsSet => !string.IsNullOrWhiteSpace(File);

    /// <summary>True when <see cref="File"/> is a URI (<see cref="ProcessLaunch.SchemeOf"/>).</summary>
    public bool IsUri => ProcessLaunch.SchemeOf(File) is not null;

    /// <summary>
    /// "Run explorer", "Run dc64cmd.exe" (a path shows its last part), "Open ms-settings:display": what the step starts,
    /// without the arguments, for the conversion reason and anywhere the arguments must not show.
    /// </summary>
    public string Target => IsUri ? "Open " + File.Trim() : "Run " + ShortName(File.Trim());

    /// <summary>"Run explorer", "Run taskkill.exe /f /im yuzu.exe (as admin, hidden)", "Open ms-settings:display"; <see cref="UnsetSummary"/> while no program is set.</summary>
    public string Summary => Describe(Arguments.Trim());

    /// <summary>For the log (<see cref="IStep.LogSummary"/>): the program without its arguments, which may hold a secret: "Run taskkill.exe … (as admin, hidden)".</summary>
    public string LogSummary => Describe(Arguments.Trim().Length == 0 ? string.Empty : "…");

    private string Describe(string arguments)
    {
        if (!IsSet)
        {
            return UnsetSummary;
        }

        var text = arguments.Length == 0 ? Target : Target + " " + arguments;
        return (Elevated, Hidden) switch
        {
            (true, true) => text + " (as admin, hidden)",
            (true, false) => text + " (as admin)",
            (false, true) => text + " (hidden)",
            _ => text,
        };
    }

    /// <summary>The launcher's request: the step's values with surrounding blanks trimmed.</summary>
    public ProcessLaunch ToLaunch() => new(File.Trim(), Arguments.Trim(), WorkingDirectory.Trim(), Elevated, Hidden);

    /// <summary>The part after the last <c>\</c> or <c>/</c> (a trailing one ignored), so a long path reads as its program; a bare name as is.</summary>
    private static string ShortName(string file)
    {
        var trimmed = file.TrimEnd('\\', '/');
        if (trimmed.Length == 0)
        {
            return file;
        }

        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }
}
