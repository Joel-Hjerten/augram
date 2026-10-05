using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class AppPathsTests
{
    [Fact]
    public void ParsesTheConfigFolderArgumentInBothForms()
    {
        var expected = Path.GetFullPath("cfg");

        Assert.Equal(expected, AppPaths.ConfigFolderFrom(["--config-folder", "cfg"]));
        Assert.Equal(expected, AppPaths.ConfigFolderFrom(["--gallery", "--CONFIG-FOLDER=cfg"]));
        Assert.Equal(expected, AppPaths.ConfigFolderFrom(["--config-folder", "\"cfg\""]));
    }

    [Fact]
    public void AbsentOrEmptyArgumentMeansTheDefault()
    {
        Assert.Null(AppPaths.ConfigFolderFrom([]));
        Assert.Null(AppPaths.ConfigFolderFrom(["--gallery"]));
        Assert.Null(AppPaths.ConfigFolderFrom(["--config-folder"]));
        Assert.Null(AppPaths.ConfigFolderFrom(["--config-folder="]));
    }

    [Fact]
    public void LogsLiveBesideTheConfig()
    {
        Assert.Equal(Path.Combine(AppPaths.ConfigFolder, "logs"), AppPaths.LogsFolder);
    }
}
