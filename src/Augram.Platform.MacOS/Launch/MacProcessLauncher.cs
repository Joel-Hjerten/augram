using System.Diagnostics;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Launch;

/// <summary>
/// The macOS <see cref="IProcessLauncher"/> (the Run step): <see cref="MacStartInfo"/> decides between the executable
/// itself and <c>open</c>, <see cref="MacProcessStart"/> starts it and never waits for the program. Running as
/// administrator has no equivalent here (no UAC; an authorization prompt needs a privileged helper), so an elevated
/// launch is declined as not supported and the step skips. Nothing here throws; reasons name the file, never the arguments.
/// </summary>
public sealed class MacProcessLauncher : IProcessLauncher
{
    public const string ElevationReason = "running as administrator is not supported on macOS";

    private readonly Func<ProcessStartInfo, string, ProcessLaunchResult> _run;
    private readonly Func<string, string> _expand;
    private readonly string _home;
    private readonly Func<string, string?> _findExecutable;

    [SupportedOSPlatform("macos")]
    public MacProcessLauncher()
        : this(MacProcessStart.Run, Environment.ExpandEnvironmentVariables, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), MacProcessStart.FindExecutable)
    {
    }

    /// <param name="run">Starts what <see cref="MacStartInfo"/> built, given the file as written for its reasons; tests pass a fake, so no test ever starts a process.</param>
    /// <param name="expand">Expands <c>%VAR%</c> in the file and the Start in folder.</param>
    /// <param name="home">The home folder.</param>
    /// <param name="findExecutable">The executable a path or a bare name resolves to, or null.</param>
    internal MacProcessLauncher(Func<ProcessStartInfo, string, ProcessLaunchResult> run, Func<string, string> expand, string home, Func<string, string?> findExecutable)
    {
        _run = run;
        _expand = expand;
        _home = home;
        _findExecutable = findExecutable;
    }

    public ProcessLaunchResult Launch(ProcessLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (launch.Elevated)
        {
            return ProcessLaunchResult.NotSupported(ElevationReason);
        }

        try
        {
            return _run(MacStartInfo.For(launch, _expand, _home, _findExecutable), launch.File);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return ProcessLaunchResult.Failed($"{launch.File} could not be started: {exception.Message}");
        }
    }
}
