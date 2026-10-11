using Augram.App.Hosting.Launch;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>One parse of a launch's switches, each flag with its own rule (the rules' details are in their owners' tests).</summary>
public sealed class LaunchArgumentsTests
{
    [Fact]
    public void NoSwitches_IsAPlainLaunch()
    {
        Assert.Equal(new LaunchArguments(ConfigFolder: null, NoEngine: false, Hidden: false, Gallery: false), LaunchArguments.Parse([], null));
    }

    [Fact]
    public void ConfigFolder_InEitherForm()
    {
        Assert.Equal(Path.GetFullPath("cfg"), LaunchArguments.Parse(["--config-folder", "cfg"], null).ConfigFolder);
        Assert.Equal(Path.GetFullPath("cfg"), LaunchArguments.Parse(["--CONFIG-FOLDER=cfg"], null).ConfigFolder);
    }

    [Fact]
    public void NoEngine_FromTheArgumentOrTheVariable()
    {
        Assert.True(LaunchArguments.Parse(["--NO-ENGINE"], null).NoEngine);
        Assert.True(LaunchArguments.Parse([], "1").NoEngine);
        Assert.False(LaunchArguments.Parse([], "0").NoEngine);
    }

    [Fact]
    public void Hidden_InAnyCase()
    {
        Assert.True(LaunchArguments.Parse(["--Hidden"], null).Hidden);
        Assert.False(LaunchArguments.Parse(["--hidden-not"], null).Hidden);
    }

    [Fact]
    public void Gallery_OnlyAsWritten()
    {
        Assert.True(LaunchArguments.Parse(["--no-engine", "--gallery"], null).Gallery);
        Assert.False(LaunchArguments.Parse(["--GALLERY"], null).Gallery);
    }
}
