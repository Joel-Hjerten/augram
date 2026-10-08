using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Platform.Windows.Launch;

/// <summary>
/// The Windows <see cref="IProcessLauncher"/> (the Run step): <c>Process.Start</c> with shell execute, as
/// <see cref="ShellStartInfo"/> builds it, never waiting for the program. ShellExecuteEx itself can block, for as long
/// as a UAC prompt is open or a network path takes to answer, so the start runs on its own short-lived STA thread and
/// the command executor waits at most <see cref="DefaultWait"/>: an answer in time is the result (started, failed with
/// the Win32 error, or cancelled when the UAC prompt was declined, error 1223); no answer yet is
/// <see cref="ProcessLaunchOutcome.Started"/> with a note, and the thread logs the outcome when it arrives (source
/// <c>steps</c>). Nothing here throws; reasons name the file, never the arguments.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32ProcessLauncher : IProcessLauncher
{
    public const string ThreadName = "augram-process-start";

    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(2);

    private readonly IEventLog _log;
    private readonly Func<ProcessStartInfo, IDisposable?> _start;
    private readonly Func<string, string> _expand;
    private readonly string _home;
    private readonly TimeSpan _wait;

    public Win32ProcessLauncher(IEventLog log)
        : this(log, info => Process.Start(info), Environment.ExpandEnvironmentVariables, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DefaultWait)
    {
    }

    /// <param name="log">Where a start that answers after <paramref name="wait"/> reports.</param>
    /// <param name="start">Starts the process; tests pass a fake, so no test ever starts one.</param>
    /// <param name="expand">Expands environment variables in the file and the Start in folder.</param>
    /// <param name="home">The Start in folder when the step has none.</param>
    /// <param name="wait">How long the caller waits for the start to answer.</param>
    internal Win32ProcessLauncher(IEventLog log, Func<ProcessStartInfo, IDisposable?> start, Func<string, string> expand, string home, TimeSpan wait)
    {
        _log = log;
        _start = start;
        _expand = expand;
        _home = home;
        _wait = wait;
    }

    public ProcessLaunchResult Launch(ProcessLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var info = ShellStartInfo.For(launch, _expand, _home);
        var attempt = new Attempt();
        var thread = new Thread(() =>
        {
            if (attempt.Complete(Start(info, launch)) is { } late)
            {
                LogLate(launch, late);
            }
        })
        {
            IsBackground = true,
            Name = ThreadName,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(_wait) && attempt.Abandon())
        {
            return new ProcessLaunchResult(
                ProcessLaunchOutcome.Started,
                $"still starting after {_wait.TotalSeconds:0.#} s (an administrator prompt may be open); the outcome is logged when it arrives");
        }

        return attempt.Result!;
    }

    private ProcessLaunchResult Start(ProcessStartInfo info, ProcessLaunch launch)
    {
        try
        {
            // Null when an existing process took the request (a link opened in a running browser): still started.
            using var process = _start(info);
            return ProcessLaunchResult.Started;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ShellExecuteErrors.Cancelled)
        {
            return ProcessLaunchResult.Cancelled(launch.Elevated ? "the administrator prompt was declined" : "cancelled by the user");
        }
        catch (Win32Exception exception)
        {
            return ProcessLaunchResult.Failed(ShellExecuteErrors.Describe(launch.File, exception.NativeErrorCode));
        }
        catch (Exception exception)
        {
            // This runs on its own thread: anything escaping would end the process.
            return ProcessLaunchResult.Failed($"{launch.File} could not be started: {exception.Message}");
        }
    }

    private void LogLate(ProcessLaunch launch, ProcessLaunchResult result)
    {
        if (result.Outcome == ProcessLaunchOutcome.Started)
        {
            _log.Info("steps", "Run started late", ("file", launch.File));
        }
        else
        {
            _log.Warning("steps", "Run did not start", ("file", launch.File), ("outcome", result.Outcome), ("reason", result.Reason));
        }
    }

    /// <summary>One start's answer, shared between the start thread and the caller that may stop waiting for it.</summary>
    private sealed class Attempt
    {
        private readonly Lock _gate = new();
        private ProcessLaunchResult? _result;
        private bool _abandoned;

        public ProcessLaunchResult? Result
        {
            get
            {
                lock (_gate)
                {
                    return _result;
                }
            }
        }

        /// <summary>The caller stops waiting; false when the answer arrived meanwhile and is there to return.</summary>
        public bool Abandon()
        {
            lock (_gate)
            {
                _abandoned = _result is null;
                return _abandoned;
            }
        }

        /// <summary>Records the answer; returns it when the caller had stopped waiting, so the thread logs it instead.</summary>
        public ProcessLaunchResult? Complete(ProcessLaunchResult result)
        {
            lock (_gate)
            {
                _result = result;
                return _abandoned ? result : null;
            }
        }
    }
}
