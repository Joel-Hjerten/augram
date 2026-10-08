namespace Augram.Import.StrokesPlus;

/// <summary>
/// The seven arguments of one StrokesPlus.net <c>sp.RunProgram(fileName, arguments, verb, style, useShellExecute,
/// noWindow, waitForExit)</c> call, as <see cref="RunProgramScript"/> read them from a script: the strings with their
/// escapes applied, and <c>sp.ExpandEnvironmentVariables("%X%")</c> in the file name kept as the <c>%X%</c> text the Run
/// step expands itself. <c>verb</c> is a shell verb (<c>open</c>, <c>runas</c> for elevated, or empty), <c>style</c> a
/// window style (<c>normal</c>, <c>hidden</c>, <c>minimized</c>, <c>maximized</c>, or empty).
/// </summary>
public sealed record RunProgramCall(string FileName, string Arguments, string Verb, string Style, bool UseShellExecute, bool NoWindow, bool WaitForExit)
{
    public const string RunAsVerb = "runas";

    public const string OpenVerb = "open";

    public const string HiddenStyle = "hidden";

    /// <summary>Verb <c>runas</c>: run as administrator.</summary>
    public bool IsElevated => string.Equals(Verb.Trim(), RunAsVerb, StringComparison.OrdinalIgnoreCase);

    /// <summary>Style <c>hidden</c>.</summary>
    public bool IsHidden => string.Equals(Style.Trim(), HiddenStyle, StringComparison.OrdinalIgnoreCase);

    /// <summary>The verb is one the Run step expresses: empty, <c>open</c> or <c>runas</c> (not <c>edit</c>, <c>print</c>…).</summary>
    public bool HasPlainVerb => Verb.Trim().Length == 0 || IsElevated || string.Equals(Verb.Trim(), OpenVerb, StringComparison.OrdinalIgnoreCase);
}
