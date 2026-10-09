using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.OpenApp;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The Open app step (Joel, 2026-10-09): Augram's own window, or an app brought to the front, or started when it is not running.</summary>
public sealed class OpenAppStepTests
{
    private static readonly OpenAppStepType Type = OpenAppStepType.Instance;

    [Fact]
    public void ReadsAndWritesOnlyWhatIsSet()
    {
        Assert.Equal("{}", Type.Write(Type.CreateDefault()).ToJsonString());
        Assert.Equal("{\"augram\":true}", Type.Write(new OpenAppStep(true, string.Empty, string.Empty)).ToJsonString());

        var chrome = new OpenAppStep(false, "chrome.exe", "Google Chrome");
        var written = Type.Write(chrome);
        Assert.Equal("{\"windows\":\"chrome.exe\",\"mac\":\"Google Chrome\"}", written.ToJsonString());
        Assert.Equal(chrome, Type.Read(written));
        Assert.Equal(new OpenAppStep(false, string.Empty, string.Empty), Type.Read(new JsonObject { ["later"] = 1 }));
        Assert.Equal(StepCategory.Run, Type.Category);
    }

    [Fact]
    public void EachPlatformOpensItsOwnApp_OrTheGuessFromTheOther()
    {
        Assert.Equal("Google Chrome", new OpenAppStep(false, "chrome.exe", string.Empty).AppFor(HostPlatform.MacOS));
        Assert.Equal("chrome.exe", new OpenAppStep(false, string.Empty, "Google Chrome").AppFor(HostPlatform.Windows));
        Assert.Equal("Safari", new OpenAppStep(false, "chrome.exe", " Safari ").AppFor(HostPlatform.MacOS));
        Assert.Null(new OpenAppStep(false, "game.exe", string.Empty).AppFor(HostPlatform.MacOS));

        Assert.Equal("Open Augram", new OpenAppStep(true, "x.exe", string.Empty).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Open chrome.exe", new OpenAppStep(false, "chrome.exe", string.Empty).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Open app (none for macOS)", new OpenAppStep(false, "game.exe", string.Empty).SummaryOn(HostPlatform.MacOS));
        Assert.Equal("Open app (no app set)", Type.CreateDefault().Summary);
    }

    [Fact]
    public void ThisAppOpensAugramsWindow()
    {
        var window = new FakeAppWindow { Opens = true };
        var launcher = new FakeProcessLauncher();

        Assert.Equal(StepOutcome.Done, Run(new OpenAppStep(true, string.Empty, string.Empty), launcher, new FakeActivator(AppActivation.NotRunning), window).Outcome);
        Assert.Equal(1, window.Opened);
        Assert.Empty(launcher.Launches);

        var none = Run(new OpenAppStep(true, string.Empty, string.Empty), launcher, new FakeActivator(AppActivation.NotRunning), new FakeAppWindow());
        Assert.Equal((StepOutcome.Skipped, "Augram has no window to open here"), (none.Outcome, none.Reason));
    }

    [Fact]
    public void ARunningAppIsBroughtForward_NotStartedAgain()
    {
        var launcher = new FakeProcessLauncher();
        var activator = new FakeActivator(AppActivation.Activated);

        var result = Run(new OpenAppStep(false, "chrome.exe", "Google Chrome"), launcher, activator, new FakeAppWindow());

        Assert.Equal(StepOutcome.Done, result.Outcome);
        Assert.Single(activator.Asked);
        Assert.Empty(launcher.Launches);
    }

    [Fact]
    public void AnAppThatIsNotRunningIsStarted_AndARefusalFails()
    {
        var launcher = new FakeProcessLauncher();

        var started = Run(new OpenAppStep(false, "chrome.exe", "Google Chrome"), launcher, new FakeActivator(AppActivation.NotRunning), new FakeAppWindow());
        Assert.Equal(StepOutcome.Done, started.Outcome);
        Assert.Equal([OpenAppStepTypeAccess.Here == HostPlatform.MacOS ? "Google Chrome" : "chrome.exe"], launcher.Launches.Select(launch => launch.File));

        var refused = Run(new OpenAppStep(false, "chrome.exe", "Google Chrome"), new FakeProcessLauncher(), new FakeActivator(AppActivation.Failed("no")), new FakeAppWindow());
        Assert.Equal((StepOutcome.Failed, "no"), (refused.Outcome, refused.Reason));

        var unset = Run((OpenAppStep)Type.CreateDefault(), new FakeProcessLauncher(), new FakeActivator(AppActivation.Activated), new FakeAppWindow());
        Assert.Equal((StepOutcome.Skipped, "no app set"), (unset.Outcome, unset.Reason));
    }

    private static StepResult Run(OpenAppStep step, FakeProcessLauncher launcher, FakeActivator activator, FakeAppWindow window)
        => Type.Execute(step, StepContexts.Create(log: NullEventLog.Instance) with { Processes = launcher, Apps = activator, AppWindow = window });

    private static class OpenAppStepTypeAccess
    {
        public static HostPlatform Here => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;
    }

    private sealed class FakeActivator(AppActivation answer) : IAppActivator
    {
        public List<string> Asked { get; } = [];

        public AppActivation BringToFront(string executable)
        {
            Asked.Add(executable);
            return answer;
        }
    }

    private sealed class FakeAppWindow : IAppWindow
    {
        public bool Opens { get; init; }

        public int Opened { get; private set; }

        public bool Open()
        {
            Opened += Opens ? 1 : 0;
            return Opens;
        }
    }
}
