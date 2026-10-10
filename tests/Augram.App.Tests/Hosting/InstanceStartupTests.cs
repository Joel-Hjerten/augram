using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Xunit;
using static Augram.App.Tests.Support.InstanceTesting;

namespace Augram.App.Tests.Hosting;

/// <summary>
/// A launch settling with a running Augram (Joel, 2026-10-08: only one at a time, the development or the installed build):
/// the same install is shown, a different build is asked about, a confirmed take-over waits for the running one to let go,
/// Cancel shows it, a timeout says so, and an Augram from before identities is still shown. Real mutex and pipe under a
/// unique name per test; the dialog is a fake.
/// </summary>
public sealed class InstanceStartupTests
{
    [Fact]
    public void WithNothingRunning_ItRuns()
    {
        using var startup = new InstanceStartup(UniqueName(), Dev);

        Assert.Equal(InstanceStartupStep.Run, startup.Begin());
        Assert.NotNull(startup.Guard);
        Assert.False(startup.NeedsChoice);
    }

    [Fact]
    public void TheSameInstall_ShowsTheRunningOne_AndExits()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Dev);
        Assert.NotNull(running);
        using var shown = new ManualResetEventSlim();
        InstanceRequestEventArgs? asked = null;
        var quits = 0;
        running.ShowRequested += (_, e) =>
        {
            asked = e;
            shown.Set();
        };
        running.QuitRequested += (_, _) => Interlocked.Increment(ref quits);

        // A rebuilt copy at the same path is still the same install.
        var rebuilt = Dev with { App = Dev.App with { Version = "0.2.1", Commit = "9d0e1f2" } };
        using var startup = new InstanceStartup(name, rebuilt);

        Assert.Equal(InstanceStartupStep.Exit, startup.Begin());
        Assert.True(shown.Wait(Wait), "the running Augram was not shown");
        Assert.Equal(InstanceStartup.SameInstallReason, asked?.Reason);
        Assert.Equal(rebuilt, asked?.From);
        Assert.Null(startup.Guard);
        Assert.Equal(0, Volatile.Read(ref quits));
    }

    [Fact]
    public void AHiddenLaunchOfTheSameInstall_ExitsWithoutShowingTheRunningOne()
    {
        // The Windows Run entry (--hidden) fires some seconds after sign-in, maybe after the user started Augram and closed its window.
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Dev);
        Assert.NotNull(running);
        using var introduced = new ManualResetEventSlim();
        var shows = 0;
        running.Introduced += (_, _) => introduced.Set();
        running.ShowRequested += (_, _) => Interlocked.Increment(ref shows);
        using var startup = new InstanceStartup(name, Dev) { IsHiddenLaunch = true };

        Assert.Equal(InstanceStartupStep.Exit, startup.Begin());

        Assert.True(introduced.Wait(Wait));
        Assert.Equal(0, Volatile.Read(ref shows));
        Assert.Null(startup.Guard);
    }

    [Fact]
    public void AHiddenLaunch_WithADifferentBuildRunning_ExitsWithoutAsking()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Dev);
        Assert.NotNull(running);
        var shows = 0;
        running.ShowRequested += (_, _) => Interlocked.Increment(ref shows);
        using var startup = new InstanceStartup(name, Installed) { IsHiddenLaunch = true };

        Assert.Equal(InstanceStartupStep.Exit, startup.Begin());

        Assert.False(startup.NeedsChoice);
        Assert.Null(startup.Guard);
        Assert.Equal(0, Volatile.Read(ref shows));
    }

    [Fact]
    public void ADifferentChannel_IsAskedAbout_AndNothingIsShownYet()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(running);
        using var introduced = new ManualResetEventSlim();
        var shows = 0;
        running.Introduced += (_, _) => introduced.Set();
        running.ShowRequested += (_, _) => Interlocked.Increment(ref shows);
        using var startup = new InstanceStartup(name, Dev);

        Assert.Equal(InstanceStartupStep.Choose, startup.Begin());

        Assert.True(startup.NeedsChoice);
        Assert.Equal(Installed, startup.Running);
        Assert.Null(startup.Guard);
        Assert.True(introduced.Wait(Wait));
        Assert.Equal(0, Volatile.Read(ref shows));
        Assert.Equal("Augram 0.2.0 (installed) is already running. Quit it and start Augram (Dev) instead?", InstanceStartup.Question(Installed, Dev));
        Assert.Equal("Augram (Dev) 0.2.0 is already running. Quit it and start the installed Augram instead?", InstanceStartup.Question(Dev, Installed));
    }

    [Fact]
    public void AnotherExecutableOfTheSameChannel_IsAskedAbout_WithBothPaths()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Dev);
        Assert.NotNull(running);
        using var startup = new InstanceStartup(name, OtherDev);

        Assert.Equal(InstanceStartupStep.Choose, startup.Begin());

        var question = InstanceStartup.Question(Dev, OtherDev);
        Assert.StartsWith("Augram (Dev) 0.2.0 is already running from another folder. Quit it and start this one instead?", question, StringComparison.Ordinal);
        Assert.Contains("Running: " + Dev.ExecutablePath, question, StringComparison.Ordinal);
        Assert.Contains("This one: " + OtherDev.ExecutablePath, question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirm_AsksTheRunningOneToQuit_WaitsForTheName_AndContinues()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(running);
        InstanceRequestEventArgs? asked = null;
        running.QuitRequested += (_, e) =>
        {
            asked = e;
            // The running app shuts down on its UI thread, then Program releases the name last.
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                running.Dispose();
            });
        };
        var presenter = new FakeTakeOverPresenter { Answer = true };
        using var startup = new InstanceStartup(name, Dev);
        Assert.Equal(InstanceStartupStep.Choose, startup.Begin());

        Assert.True(await startup.ChooseAsync(presenter));

        var (title, message, confirm) = Assert.Single(presenter.Questions);
        Assert.Equal("Augram (Dev)", title);
        Assert.Equal(InstanceStartup.Question(Installed, Dev), message);
        Assert.Equal("Quit it and start this one", confirm);
        Assert.Empty(presenter.Messages);
        Assert.NotNull(startup.Guard);
        Assert.False(startup.NeedsChoice);
        Assert.Equal(Dev, asked?.From);

        // This launch now owns the name and answers the next one.
        var third = SingleInstanceGuard.Send(name, new InstanceRequest(InstanceRequestKind.Hello, OtherDev), TimeSpan.FromSeconds(5));
        Assert.Equal(Dev, third.Running);
        var log = new ListEventLog();
        startup.LogNotes(log);
        var note = Assert.Single(log.Events);
        Assert.Equal("Took over from another Augram", note.Message);
        Assert.Contains(note.Properties!, p => p.Key == "running" && Equals(p.Value, "Augram 0.2.0 (installed)"));
    }

    [Fact]
    public async Task Cancel_ShowsTheRunningOne_AndExits()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(running);
        using var shown = new ManualResetEventSlim();
        InstanceRequestEventArgs? asked = null;
        var quits = 0;
        running.ShowRequested += (_, e) =>
        {
            asked = e;
            shown.Set();
        };
        running.QuitRequested += (_, _) => Interlocked.Increment(ref quits);
        var presenter = new FakeTakeOverPresenter { Answer = false };
        using var startup = new InstanceStartup(name, Dev);
        Assert.Equal(InstanceStartupStep.Choose, startup.Begin());

        Assert.False(await startup.ChooseAsync(presenter));

        Assert.True(shown.Wait(Wait), "the running Augram was not shown after Cancel");
        Assert.Equal(InstanceStartup.CancelledReason, asked?.Reason);
        Assert.Equal(0, Volatile.Read(ref quits));
        Assert.Null(startup.Guard);
        using var still = SingleInstanceGuard.TryAcquire(name, OtherDev);
        Assert.Null(still);
    }

    [Fact]
    public async Task ARunningOneThatDoesNotQuitInTime_IsReported_AndThisLaunchExits()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(running);
        using var quitAsked = new ManualResetEventSlim();
        running.QuitRequested += (_, _) => quitAsked.Set();
        var presenter = new FakeTakeOverPresenter { Answer = true };
        using var startup = new InstanceStartup(name, Dev, quitTimeout: TimeSpan.FromMilliseconds(300));
        Assert.Equal(InstanceStartupStep.Choose, startup.Begin());

        Assert.False(await startup.ChooseAsync(presenter));

        Assert.True(quitAsked.Wait(Wait));
        var (title, message) = Assert.Single(presenter.Messages);
        Assert.Equal("Augram (Dev)", title);
        Assert.Equal(InstanceStartup.TimeoutMessage(Installed, Dev, startup.QuitTimeout), message);
        Assert.Null(startup.Guard);
        using var still = SingleInstanceGuard.TryAcquire(name, OtherDev);
        Assert.Null(still);
        Assert.Equal(
            "Augram 0.2.0 (installed) did not quit within 10 seconds. Quit it from its tray icon, then start Augram (Dev) again.",
            InstanceStartup.TimeoutMessage(Installed, Dev, InstanceStartup.DefaultQuitTimeout));
    }

    [Fact]
    public async Task AnAugram01_IsShown_AndThisLaunchExits()
    {
        var name = UniqueName();
        using var held = HoldName(name);
        using var cancel = new CancellationTokenSource(Wait);
        var connected = ListenLikeAugram01(name, cancel.Token);
        using var startup = new InstanceStartup(name, Dev);

        Assert.Equal(InstanceStartupStep.Exit, startup.Begin());

        Assert.True(await connected, "the old Augram was not reached");
        Assert.Null(startup.Running);
        Assert.Null(startup.Guard);
    }

    [Fact]
    public async Task ASilentHolder_IsGivenAMomentToLetGo()
    {
        var name = UniqueName();
        var held = HoldName(name);
        var release = Task.Run(async () =>
        {
            await Task.Delay(300);
            held.Dispose();
        });
        using var startup = new InstanceStartup(name, Dev, silentWait: Wait);

        Assert.Equal(InstanceStartupStep.Run, startup.Begin());

        await release;
        Assert.NotNull(startup.Guard);
    }

    [Fact]
    public void ASilentHolderThatKeepsTheName_EndsThisLaunch()
    {
        var name = UniqueName();
        using var held = HoldName(name);
        using var startup = new InstanceStartup(name, Dev, silentWait: TimeSpan.FromMilliseconds(200));

        Assert.Equal(InstanceStartupStep.Exit, startup.Begin());
        Assert.Null(startup.Guard);
    }
}
