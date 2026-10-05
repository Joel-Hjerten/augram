using Augram.Core.Config;

namespace Augram.App.Hosting;

/// <summary>Where the app keeps its files: the Core config folder and <c>logs/</c> beside it (N4).</summary>
public static class AppPaths
{
    public static string ConfigFolder => DefaultConfigFolder.Resolve();

    public static string LogsFolder => Path.Combine(ConfigFolder, "logs");
}
