namespace Augram.Core.Abstractions;

/// <summary>The platform's name as a person reads it, and the other platform: the one place both are written.</summary>
public static class HostPlatformExtensions
{
    /// <summary>"Windows" or "macOS".</summary>
    public static string DisplayName(this HostPlatform platform) => platform == HostPlatform.MacOS ? "macOS" : "Windows";

    /// <summary>The platform that is not <paramref name="platform"/>: macOS for Windows, Windows for macOS.</summary>
    public static HostPlatform Other(this HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;
}
