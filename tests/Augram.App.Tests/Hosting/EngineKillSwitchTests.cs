using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class EngineKillSwitchTests
{
    [Fact]
    public void ArgumentOrEnvironmentVariableDisablesTheEngine()
    {
        Assert.True(EngineKillSwitch.IsSet(["--no-engine"], null));
        Assert.True(EngineKillSwitch.IsSet(["--gallery", "--NO-ENGINE"], null));
        Assert.True(EngineKillSwitch.IsSet([], "1"));
        Assert.True(EngineKillSwitch.IsSet([], " 1 "));
    }

    [Fact]
    public void OtherwiseTheEngineRuns()
    {
        Assert.False(EngineKillSwitch.IsSet([], null));
        Assert.False(EngineKillSwitch.IsSet(["--gallery"], string.Empty));
        Assert.False(EngineKillSwitch.IsSet([], "0"));
        Assert.False(EngineKillSwitch.IsSet([], "true"));
    }
}
