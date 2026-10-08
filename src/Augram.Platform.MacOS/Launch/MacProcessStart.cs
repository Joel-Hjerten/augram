using System.Diagnostics;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Launch;

/// <summary>
/// The native half of <see cref="MacProcessLauncher"/>, untested because it starts real processes: finding an executable
/// on disk or on the PATH, and starting what <see cref="MacStartInfo"/> built. An executable is started and left alone.
/// <c>open</c> is waited for (not the app it opens; <c>open</c> returns once LaunchServices took the request) up to
/// <see cref="OpenWait"/>, so a name LaunchServices does not know fails with <c>open</c>'s own message.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacProcessStart
{
    public static readonly TimeSpan OpenWait = TimeSpan.FromSeconds(2);

    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>A path to an executable file as is; a bare name searched on the PATH; null for anything else (a bundle is a folder, not a file).</summary>
    public static string? FindExecutable(string file)
    {
        if (file.Contains('/', StringComparison.Ordinal))
        {
            return IsExecutableFile(file) ? file : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder, file);
            if (IsExecutableFile(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static ProcessLaunchResult Run(ProcessStartInfo info, string file)
    {
        using var process = Process.Start(info);
        if (process is null || info.FileName != MacStartInfo.OpenPath)
        {
            return ProcessLaunchResult.Started;
        }

        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(OpenWait))
        {
            return new ProcessLaunchResult(ProcessLaunchOutcome.Started, $"open has not answered after {OpenWait.TotalSeconds:0} s");
        }

        if (process.ExitCode == 0)
        {
            return ProcessLaunchResult.Started;
        }

        var message = error.Wait(TimeSpan.FromMilliseconds(200)) ? FirstLine(error.Result) : string.Empty;
        return ProcessLaunchResult.Failed(message.Length == 0
            ? $"{file} could not be opened (open exited with {process.ExitCode})"
            : $"{file} could not be opened: {message}");
    }

    private static bool IsExecutableFile(string path)
    {
        try
        {
            return File.Exists(path) && (File.GetUnixFileMode(path) & AnyExecute) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var end = trimmed.IndexOfAny(['\r', '\n']);
        return end < 0 ? trimmed : trimmed[..end];
    }
}
