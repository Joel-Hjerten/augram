using System.ComponentModel;
using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Launch;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Launch;

/// <summary>The macOS launcher over a fake runner (no test starts a process): elevation declined, answers passed through, errors caught.</summary>
public sealed class MacProcessLauncherTests
{
    [Fact]
    public void ElevationIsNotSupportedAndNothingRuns()
    {
        var runs = 0;
        var launcher = Launcher((_, _) =>
        {
            runs++;
            return ProcessLaunchResult.Started;
        });

        var result = launcher.Launch(new ProcessLaunch("/usr/bin/killall", "Synthetic", Elevated: true));

        Assert.Equal(ProcessLaunchResult.NotSupported("running as administrator is not supported on macOS"), result);
        Assert.Equal(0, runs);
    }

    [Fact]
    public void TheRunnerGetsTheStartInfoAndTheFileAndItsAnswerIsTheResult()
    {
        (ProcessStartInfo Info, string File)? seen = null;
        var launcher = Launcher((info, file) =>
        {
            seen = (info, file);
            return ProcessLaunchResult.Failed("Safari2 could not be opened: Unable to find application named 'Safari2'");
        });

        var result = launcher.Launch(new ProcessLaunch("Safari2", "--private", Hidden: true));

        Assert.Equal(ProcessLaunchResult.Failed("Safari2 could not be opened: Unable to find application named 'Safari2'"), result);
        Assert.Equal(MacStartInfo.OpenPath, seen!.Value.Info.FileName);
        Assert.Equal("-g -j -a Safari2 --args --private", seen.Value.Info.Arguments);
        Assert.Equal("Safari2", seen.Value.File);
    }

    [Fact]
    public void AStartErrorFailsNamingTheFileOnly()
    {
        var launcher = Launcher((_, _) => throw new Win32Exception(13, "Permission denied"));

        var result = launcher.Launch(new ProcessLaunch("/Users/synthetic/bin/tool", "--token hunter2"));

        Assert.Equal(ProcessLaunchResult.Failed("/Users/synthetic/bin/tool could not be started: Permission denied"), result);
    }

    private static MacProcessLauncher Launcher(Func<ProcessStartInfo, string, ProcessLaunchResult> run)
        => new(run, text => text, "/Users/synthetic", file => file.StartsWith("/Users/synthetic/bin/", StringComparison.Ordinal) ? file : null);
}
