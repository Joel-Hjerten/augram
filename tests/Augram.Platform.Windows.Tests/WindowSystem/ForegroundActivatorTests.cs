using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>
/// Technique order, thread attach/detach pairing, verification and logging (B1): one line per technique that did not settle;
/// the outcome is the engine's line (plan 0007 Q7), never this adapter's.
/// </summary>
public sealed class ForegroundActivatorTests
{
    private const nint Target = 0x10;
    private const nint Other = 0x20;

    [Fact]
    public void PlainCallWorks_FirstTechniqueWins()
    {
        var (win, log) = Setup(plain: true);

        var result = Activate(win, log);

        Assert.True(result.Succeeded);
        Assert.Equal("set-foreground", result.Technique);
        Assert.Equal(["set:16:attached=False:alt=False"], win.Calls);
        Assert.Empty(log.Events);
    }

    [Fact]
    public void PlainFails_AttachThreadInputIsPairedAndWins()
    {
        var (win, log) = Setup(attached: true);

        var result = Activate(win, log);

        Assert.Equal("attach-thread-input", result.Technique);
        Assert.Equal(
            ["set:16:attached=False:alt=False", "attach:5->1:True", "set:16:attached=True:alt=False", "attach:5->1:False"],
            win.Calls);
        Assert.False(win.Attached);
        var e = Assert.Single(log.Events);
        Assert.Equal(LogSources.Window, e.Source);
        Assert.Equal(EventLevel.Info, e.Level);
        Assert.Equal("Activation attempt failed", e.Message);
        Assert.Equal("set-foreground", ListEventLog.Property(e, "technique"));
        Assert.Equal("app.exe", ListEventLog.Property(e, "process"));
    }

    [Fact]
    public void AttachFails_AltTapIsLastResort()
    {
        var (win, log) = Setup(altTap: true);

        var result = Activate(win, log);

        Assert.Equal("alt-tap", result.Technique);
        Assert.Contains("alt-tap", win.Calls);
        Assert.Equal(2, log.Events.Count);
    }

    [Fact]
    public void NothingWorks_FailsWithOneLinePerTechniqueAndNoStrayAttach()
    {
        var (win, log) = Setup();

        var result = Activate(win, log);

        Assert.False(result.Succeeded);
        Assert.Equal("none", result.Technique);
        Assert.False(win.Attached);
        Assert.Equal(["set-foreground", "attach-thread-input", "alt-tap"], log.Events.Select(e => ListEventLog.Property(e, "technique")));
        Assert.All(log.Events, e => Assert.Equal(("Activation attempt failed", EventLevel.Info), (e.Message, e.Level)));
    }

    [Fact]
    public void Verification_PollsUntilSettleBudgetThenGivesUp()
    {
        var (win, log) = Setup();
        var slept = 0;

        new ForegroundActivator(win, win, log, ms => slept += ms).Activate(Identity());

        // Three techniques, each polled for up to 50 ms in 5 ms steps.
        Assert.Equal(150, slept);
    }

    [Fact]
    public void ForegroundIsOurOwnThread_NoAttachAttempted()
    {
        var (win, log) = Setup();
        win.OurThread = 5;

        Activate(win, log);

        Assert.DoesNotContain(win.Calls, c => c.StartsWith("attach", StringComparison.Ordinal));
    }

    private static (FakeWin32 Win, ListEventLog Log) Setup(bool plain = false, bool attached = false, bool altTap = false)
    {
        var win = new FakeWin32 { Foreground = Other, PlainSucceeds = plain, AttachedSucceeds = attached, AltTapSucceeds = altTap }
            .AddWindow(Target, "T", pid: 1, thread: 9)
            .AddWindow(Other, "O", pid: 2, thread: 5);
        return (win, new ListEventLog());
    }

    private static ActivationResult Activate(FakeWin32 win, ListEventLog log)
        => new ForegroundActivator(win, win, log, _ => { }).Activate(Identity());

    private static WindowIdentity Identity() => new(Target, Target, "app.exe", null, null, [], 1, false, false);
}
