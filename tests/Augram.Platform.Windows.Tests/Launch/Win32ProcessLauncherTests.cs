using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Platform.Windows.Launch;
using Xunit;

namespace Augram.Platform.Windows.Tests.Launch;

/// <summary>
/// The Windows launcher over a fake start delegate: no test starts a process. Covers the answers ShellExecuteEx gives
/// (started, declined UAC prompt, Win32 errors), the STA start thread, and a start that answers after the bounded wait.
/// </summary>
public sealed class Win32ProcessLauncherTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    [Fact]
    public void AStartedProgramIsStartedAndItsHandleDisposed()
    {
        var handle = new Handle();
        ProcessStartInfo? seen = null;
        var launcher = Launcher(info =>
        {
            seen = info;
            return handle;
        });

        var result = launcher.Launch(new ProcessLaunch("explorer"));

        Assert.Equal(ProcessLaunchResult.Started, result);
        Assert.Equal("explorer", seen!.FileName);
        Assert.True(handle.Disposed);
    }

    [Fact]
    public void NoProcessBackIsStillStarted()
    {
        // A link opened in a running browser: ShellExecuteEx succeeds and hands back no process.
        Assert.Equal(ProcessLaunchResult.Started, Launcher(_ => null).Launch(new ProcessLaunch("https://example.com")));
    }

    [Fact]
    public void TheStartRunsOnItsOwnStaThread()
    {
        (ApartmentState Apartment, string? Name, bool Background) seen = default;
        var launcher = Launcher(_ =>
        {
            seen = (Thread.CurrentThread.GetApartmentState(), Thread.CurrentThread.Name, Thread.CurrentThread.IsBackground);
            return null;
        });

        launcher.Launch(new ProcessLaunch("explorer"));

        Assert.Equal((ApartmentState.STA, Win32ProcessLauncher.ThreadName, true), seen);
    }

    [Fact]
    public void ADeclinedPromptIsCancelledNotFailed()
    {
        var launcher = Launcher(_ => throw new Win32Exception(1223));

        Assert.Equal(ProcessLaunchResult.Cancelled("the administrator prompt was declined"), launcher.Launch(new ProcessLaunch("taskkill.exe", Elevated: true)));
        Assert.Equal(ProcessLaunchResult.Cancelled("cancelled by the user"), launcher.Launch(new ProcessLaunch("taskkill.exe")));
    }

    [Theory]
    [InlineData(2, "explorer2 was not found (2)")]
    [InlineData(3, "the path of explorer2 was not found (3)")]
    [InlineData(5, "explorer2: access denied (5)")]
    [InlineData(267, "explorer2: the Start in folder is not valid (267)")]
    [InlineData(1155, "explorer2: no app is associated with it (1155)")]
    public void Win32ErrorsFailWithAReasonNamingTheFile(int error, string expected)
    {
        var result = Launcher(_ => throw new Win32Exception(error)).Launch(new ProcessLaunch("explorer2", "--secret hunter2"));

        Assert.Equal(ProcessLaunchResult.Failed(expected), result);
    }

    [Fact]
    public void OtherErrorsFailToo()
    {
        var unknown = Launcher(_ => throw new Win32Exception(1234)).Launch(new ProcessLaunch("tool.exe", "--secret hunter2"));
        Assert.Equal(ProcessLaunchOutcome.Failed, unknown.Outcome);
        Assert.StartsWith("tool.exe could not be started: ", unknown.Reason, StringComparison.Ordinal);
        Assert.EndsWith("(1234)", unknown.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", unknown.Reason, StringComparison.Ordinal);

        var invalid = Launcher(_ => throw new InvalidOperationException("No file name was specified.")).Launch(new ProcessLaunch("tool.exe"));
        Assert.Equal(ProcessLaunchResult.Failed("tool.exe could not be started: No file name was specified."), invalid);
    }

    [Fact]
    public void ASlowStartAnswersStartedAfterTheWaitAndLogsTheLateOutcome()
    {
        using var release = new ManualResetEventSlim();
        var log = new ConcurrentLog();
        var launcher = Launcher(_ => release.Wait(Generous) ? throw new Win32Exception(1223) : (IDisposable?)null, log, TimeSpan.FromMilliseconds(20));

        var result = launcher.Launch(new ProcessLaunch("taskkill.exe", "/f /im synthetic-emulator.exe", Elevated: true));

        Assert.Equal(ProcessLaunchOutcome.Started, result.Outcome);
        Assert.StartsWith("still starting after", result.Reason, StringComparison.Ordinal);
        Assert.Empty(log.Events);

        release.Set();
        var late = log.WaitForOne();
        Assert.Equal(EventLevel.Warning, late.Level);
        Assert.Equal("steps", late.Source);
        Assert.Equal("Run did not start", late.Message);
        Assert.Contains(new LogProperty("file", "taskkill.exe"), late.Properties!);
        Assert.Contains(new LogProperty("outcome", ProcessLaunchOutcome.Cancelled), late.Properties!);
        Assert.Contains(new LogProperty("reason", "the administrator prompt was declined"), late.Properties!);
    }

    [Fact]
    public void ALateSuccessIsLoggedAsInfo()
    {
        using var release = new ManualResetEventSlim();
        var log = new ConcurrentLog();
        var launcher = Launcher(_ => release.Wait(Generous) ? (IDisposable?)null : throw new TimeoutException(), log, TimeSpan.FromMilliseconds(20));

        Assert.Equal(ProcessLaunchOutcome.Started, launcher.Launch(new ProcessLaunch(@"\\server\share\tool.exe")).Outcome);

        release.Set();
        var late = log.WaitForOne();
        Assert.Equal(EventLevel.Info, late.Level);
        Assert.Equal("Run started late", late.Message);
    }

    private static Win32ProcessLauncher Launcher(Func<ProcessStartInfo, IDisposable?> start, IEventLog? log = null, TimeSpan? wait = null)
        => new(log ?? NullEventLog.Instance, start, text => text, @"C:\Users\synthetic", wait ?? Generous);

    private sealed class Handle : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    /// <summary>A log the start thread writes to while the test thread polls it.</summary>
    private sealed class ConcurrentLog : IEventLog
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public IReadOnlyList<LogEvent> Events => [.. _events];

        public bool IsEnabled(EventLevel level) => true;

        public void Log(LogEvent e) => _events.Enqueue(e);

        public LogEvent WaitForOne()
        {
            var deadline = DateTime.UtcNow + Generous;
            while (_events.IsEmpty && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }

            return Assert.Single(Events);
        }
    }
}
