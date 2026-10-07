using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Augram.Sync.Git;

/// <summary>
/// Runs the installed git: the only place in this project that starts a process. Security rules
/// (requirements F8 sync, "no credentials in Augram"):
/// <list type="bullet">
/// <item>arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, never a joined command string, and no shell;</item>
/// <item><c>GIT_TERMINAL_PROMPT=0</c> and no <c>GIT_ASKPASS</c>: git must never wait on a prompt nobody can see
/// (the credential helper's own GUI sign-in, e.g. Git Credential Manager, still works); stdin is closed at once;</item>
/// <item>every call has a timeout and a late process is killed with its whole tree;</item>
/// <item><c>LC_ALL=C</c> so <see cref="GitFailure"/> can read git's messages in English on any system language;</item>
/// <item>inherited <c>GIT_DIR</c>-style variables are dropped and <c>GIT_CEILING_DIRECTORIES</c> is the working
/// folder's parent, so a command can never act on a repository other than the clone (or, for <c>clone</c>, nothing).</item>
/// </list>
/// </summary>
internal sealed class GitRunner
{
    public static readonly TimeSpan LocalTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan DrainWait = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // Variables that would point git at another repository, index or object store than the clone.
    private static readonly string[] RedirectVariables =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_PREFIX",
    ];

    private readonly IReadOnlyDictionary<string, string?> _environment;

    /// <param name="executable">The git executable: <c>git</c> (found on PATH) or a full path.</param>
    /// <param name="environment">Extra variables for every call, applied last; a null value removes the
    /// variable. Tests isolate git's global and system config with this; the App passes nothing.</param>
    public GitRunner(string executable, IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        Executable = executable;
        _environment = environment ?? new Dictionary<string, string?>();
    }

    public string Executable { get; }

    /// <summary>Runs git once in <paramref name="workingDirectory"/> (which must exist) and waits at most <paramref name="timeout"/>.</summary>
    public GitResult Run(string workingDirectory, TimeSpan timeout, IReadOnlyList<string> arguments)
    {
        var start = CreateStartInfo(workingDirectory, arguments);
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return GitResult.NotInstalled;
        }

        if (process is null)
        {
            return GitResult.NotInstalled;
        }

        using (process)
        {
            CloseInput(process);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                Stop(process);
                return new GitResult(GitRunStatus.TimedOut, -1, Collect(output), Collect(error));
            }

            return new GitResult(GitRunStatus.Completed, process.ExitCode, Collect(output), Collect(error));
        }
    }

    private ProcessStartInfo CreateStartInfo(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        var environment = start.Environment;
        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment.Remove("GIT_ASKPASS");
        environment["LC_ALL"] = "C";
        foreach (var name in RedirectVariables)
        {
            environment.Remove(name);
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(workingDirectory));
        if (!string.IsNullOrEmpty(parent))
        {
            environment["GIT_CEILING_DIRECTORIES"] = parent;
        }

        foreach (var (name, value) in _environment)
        {
            if (value is null)
            {
                environment.Remove(name);
            }
            else
            {
                environment[name] = value;
            }
        }

        return start;
    }

    private static void CloseInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // git already exited; there was nothing to close.
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(DrainWait);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone, or not ours to stop; the reads below are bounded either way.
        }
    }

    // A grandchild that outlives git (rare) can hold the pipes open; never wait on it for long.
    private static string Collect(Task<string> read)
    {
        try
        {
            return read.Wait(DrainWait) ? read.Result : string.Empty;
        }
        catch (AggregateException)
        {
            return string.Empty;
        }
    }
}
