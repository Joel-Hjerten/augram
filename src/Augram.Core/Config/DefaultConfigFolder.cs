namespace Augram.Core.Config;

/// <summary>
/// The per-platform app-data folder a fresh install uses (checklist A17; the user may point
/// the app at another folder). Only <see cref="Environment"/> is consulted, so Core stays OS-free.
/// </summary>
public static class DefaultConfigFolder
{
    public const string FolderName = "Augram";

    public static string Resolve()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", FolderName);
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrEmpty(xdgConfigHome) ? Path.Combine(home, ".config") : xdgConfigHome;
        return Path.Combine(configHome, FolderName);
    }
}
