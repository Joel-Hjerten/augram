using Augram.App.Hosting;

namespace Augram.App.Tests.Support;

/// <summary>Fixed builds for tests that depend on the channel, so they do not depend on how the test run itself was built.</summary>
internal static class TestBuilds
{
    public static AppInfo Release { get; } = new("0.2.0", "3f1c2ab", AppChannel.Release);

    public static AppInfo Dev { get; } = new("0.2.0", "3f1c2ab", AppChannel.Dev);
}
