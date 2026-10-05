using Augram.Core.Config;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class DefaultConfigFolderTests
{
    [Fact]
    public void ResolvesToARootedAugramFolder()
    {
        var folder = DefaultConfigFolder.Resolve();

        Assert.True(Path.IsPathRooted(folder));
        Assert.Equal(DefaultConfigFolder.FolderName, Path.GetFileName(folder));
        if (OperatingSystem.IsWindows())
        {
            Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
