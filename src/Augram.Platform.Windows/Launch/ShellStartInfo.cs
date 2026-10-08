using System.Diagnostics;
using Augram.Core.Abstractions;

namespace Augram.Platform.Windows.Launch;

/// <summary>
/// The <see cref="ProcessStartInfo"/> for one <see cref="ProcessLaunch"/>, pure so the tests can read it without starting
/// anything. Always shell execute (ShellExecuteEx), which is what makes a bare name on the PATH or in App Paths
/// (<c>explorer</c>, <c>mspaint.exe</c>), a document, a folder and a URI (<c>ms-settings:display</c>) work like Win+R.
/// Environment variables are expanded in the file and the Start in folder, never in a URI and never in the arguments; an
/// empty Start in is the user's profile folder. Elevated is the <c>runas</c> verb (the UAC prompt), hidden is
/// <see cref="ProcessWindowStyle.Hidden"/> (SW_HIDE). No error dialog: the step reports the error instead.
/// </summary>
internal static class ShellStartInfo
{
    public const string RunAsVerb = "runas";

    public static ProcessStartInfo For(ProcessLaunch launch, Func<string, string> expand, string home)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(expand);
        return new ProcessStartInfo
        {
            FileName = launch.UriScheme is null ? expand(launch.File) : launch.File,
            Arguments = launch.Arguments,
            WorkingDirectory = launch.WorkingDirectory.Length == 0 ? home : expand(launch.WorkingDirectory),
            UseShellExecute = true,
            Verb = launch.Elevated ? RunAsVerb : string.Empty,
            WindowStyle = launch.Hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
            ErrorDialog = false,
        };
    }
}
