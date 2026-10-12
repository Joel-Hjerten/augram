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
        var time = new FakeTime();

        new ForegroundActivator(win, win, log, ms => { slept += ms; time.Advance(ms); }, time).Activate(Identity());

        // Three techniques, each polled for up to 50 ms in 5 ms steps.
        Assert.Equal(150, slept);
    }

    [Fact]
    public void SettleBudget_IsByTheClock_NotBySleepCount()
    {
        // A Windows sleep of 5 ms lasts a 16 ms timer tick (plan 0008: 153 ms per technique instead of 50).
        var (win, log) = Setup();
        var slept = 0;
        var time = new FakeTime();

        new ForegroundActivator(win, win, log, ms => { slept += 16; time.Advance(16); }, time).Activate(Identity());

        // Four ticks reach the 50 ms budget per technique: 64 ms × 3, not 160 ms × 3.
        Assert.Equal(192, slept);
    }

    [Fact]
    public void OwnedHelperOfTheTargetInFront_DoesNotCountAsSettled()
    {
        // Plan 0008: the foreground is a window the target owns; its root owner is the target, but the keys would go to the helper.
        var (win, log) = Setup();
        win.AddWindow(0x12, "Afx:helper", owner: Target, pid: 1, thread: 9);
        win.Foreground = 0x12;

        var result = Activator(win, log).Activate(Identity(), Target);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ModalDialogOfADisabledTargetInFront_CountsAsSettled()
    {
        // The Open app step on an app whose Save dialog is up: the dialog is as far forward as the app can come.
        var (win, log) = Setup();
        win.AddWindow(0x12, "SaveDialog", owner: Target, pid: 1, thread: 9);
        win.Foreground = 0x12;
        win.Disabled.Add(Target);

        var result = Activate(win, log);

        Assert.True(result.Succeeded);
        Assert.Equal("set-foreground", result.Technique);
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
        => Activator(win, log).Activate(Identity());

    /// <summary>An activator whose sleeps advance a scripted clock by the milliseconds asked for.</summary>
    private static ForegroundActivator Activator(FakeWin32 win, ListEventLog log)
    {
        var time = new FakeTime();
        return new ForegroundActivator(win, win, log, time.Advance, time);
    }

    private static WindowIdentity Identity() => new(Target, Target, "app.exe", null, null, [], 1, false, false);

    /// <summary>A clock in milliseconds that only moves when a test advances it.</summary>
    private sealed class FakeTime : TimeProvider
    {
        private long _ms;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => _ms;

        public void Advance(int ms) => _ms += ms;
    }
}
