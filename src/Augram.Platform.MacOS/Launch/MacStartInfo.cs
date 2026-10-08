using System.Diagnostics;
using System.Text;
using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Launch;

/// <summary>
/// The <see cref="ProcessStartInfo"/> for one <see cref="ProcessLaunch"/> on macOS, pure so it is tested on every OS
/// without starting anything. A URI goes to <c>open</c> (its handler). Otherwise the file is expanded (<c>%VAR%</c>, a
/// leading <c>~</c>) and: an executable file, or a bare name the PATH resolves to one, runs itself with the arguments and
/// the Start in folder (the home folder when empty); an app (a bare name such as <c>Safari</c>, or a path ending in
/// <c>.app</c>) goes to <c>open -a … --args …</c>; anything else, a document or a folder, to <c>open</c>. <c>open</c>
/// ignores the arguments for a URI, document or folder and launches through LaunchServices, which ignores the Start in
/// folder. Hidden adds <c>-g -j</c>: not brought to the front, launched hidden. Elevation never gets here (the launcher
/// declines it).
/// </summary>
internal static class MacStartInfo
{
    public const string OpenPath = "/usr/bin/open";

    /// <param name="launch">The request.</param>
    /// <param name="expand">Expands <c>%VAR%</c> in the file and the Start in folder.</param>
    /// <param name="home">The home folder: <c>~</c>, and the Start in folder of an executable when the request has none.</param>
    /// <param name="findExecutable">The executable a path or a bare name resolves to, or null (an app bundle, a document, a folder, a name not on the PATH).</param>
    public static ProcessStartInfo For(ProcessLaunch launch, Func<string, string> expand, string home, Func<string, string?> findExecutable)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(expand);
        ArgumentNullException.ThrowIfNull(findExecutable);
        if (launch.UriScheme is not null)
        {
            return Open(launch.Hidden, Quote(launch.File));
        }

        var file = ExpandPath(launch.File, expand, home);
        if (findExecutable(file) is { } executable)
        {
            return new ProcessStartInfo(executable)
            {
                Arguments = launch.Arguments,
                WorkingDirectory = launch.WorkingDirectory.Length == 0 ? home : ExpandPath(launch.WorkingDirectory, expand, home),
                UseShellExecute = false,
            };
        }

        var bundle = file.TrimEnd('/');
        if (!bundle.Contains('/', StringComparison.Ordinal) || bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            var arguments = launch.Arguments.Length == 0 ? string.Empty : " --args " + launch.Arguments;
            return Open(launch.Hidden, "-a " + Quote(bundle) + arguments);
        }

        return Open(launch.Hidden, Quote(file));
    }

    /// <summary><c>%VAR%</c> expanded, then a leading <c>~</c> or <c>~/</c> replaced by <paramref name="home"/>.</summary>
    public static string ExpandPath(string path, Func<string, string> expand, string home)
    {
        ArgumentNullException.ThrowIfNull(expand);
        var expanded = expand(path);
        if (expanded == "~")
        {
            return home;
        }

        return expanded.StartsWith("~/", StringComparison.Ordinal) ? home.TrimEnd('/') + expanded[1..] : expanded;
    }

    /// <summary>
    /// One argument as .NET splits an argument string into argv on Unix (the Windows command-line rules): kept as is
    /// when it has no blank, quote or backslash, otherwise in double quotes with quotes and the backslashes before them escaped.
    /// </summary>
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"', '\\']) < 0)
        {
            return argument;
        }

        var quoted = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            quoted.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes).Append(c);
            backslashes = 0;
        }

        return quoted.Append('\\', backslashes * 2).Append('"').ToString();
    }

    private static ProcessStartInfo Open(bool hidden, string arguments) => new(OpenPath)
    {
        Arguments = hidden ? "-g -j " + arguments : arguments,
        UseShellExecute = false,
        RedirectStandardError = true,
    };
}
