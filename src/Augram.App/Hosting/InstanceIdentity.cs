namespace Augram.App.Hosting;

/// <summary>
/// One running or starting Augram as the single-instance protocol tells them apart: its build (<see cref="AppInfo"/>:
/// version, commit, channel) and the executable it runs from. Two launches are the same install when channel and
/// executable match (<see cref="IsSameInstallAs"/>); then a second launch only shows the first. Anything else (the
/// installed build against a development one, or two development builds in different folders) asks the user first.
/// </summary>
public sealed record InstanceIdentity(AppInfo App, string ExecutablePath)
{
    /// <summary>This process: <see cref="AppInfo.Current"/> and its own executable.</summary>
    public static InstanceIdentity Current(AppInfo? app = null) => new(app ?? AppInfo.Current, Environment.ProcessPath ?? string.Empty);

    /// <summary>
    /// Same channel and same executable. Paths compare without case on Windows and macOS (both file systems ignore it by
    /// default), after resolving them; the version may differ (a rebuilt development copy is still the same install).
    /// </summary>
    public bool IsSameInstallAs(InstanceIdentity other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return App.Channel == other.App.Channel && PathsEqual(ExecutablePath, other.ExecutablePath);
    }

    private static bool PathsEqual(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Normalize(a), Normalize(b), comparison);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
