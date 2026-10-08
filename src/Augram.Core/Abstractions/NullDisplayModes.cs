namespace Augram.Core.Abstractions;

/// <summary>Knows no displays and changes nothing; the default for tests and for a platform without an adapter yet.</summary>
public sealed class NullDisplayModes : IDisplayModes
{
    public static NullDisplayModes Instance { get; } = new();

    private NullDisplayModes()
    {
    }

    public HostPlatform Platform => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;

    public bool CanSwitchHdr => false;

    public IReadOnlyList<DisplayInfo> Displays() => [];

    public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode)
        => DisplayChangeResult.Failed($"changing display modes is not supported on {Platform}");

    public DisplayChangeResult SetHdr(DisplayInfo display, bool on)
        => DisplayChangeResult.Failed($"switching HDR is not supported on {Platform}");
}
