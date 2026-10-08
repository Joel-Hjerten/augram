#if DEBUG
using Augram.Core.Abstractions;

namespace Augram.App.DevGallery;

/// <summary>A pretend 4K TV like Joel's (TV and 1000/1001 rates, HDR off) so the gallery's Display mode form has lists to show; it changes nothing.</summary>
internal sealed class GalleryDisplayModes : IDisplayModes
{
    private static readonly int[] LegacyRates = [23, 24, 25, 29, 30, 50, 59, 60, 100, 119, 120];

    private static readonly DisplayResolution[] Sizes = [new(3840, 2160), new(2560, 1440), new(1920, 1080), new(1280, 720)];

    public static GalleryDisplayModes Instance { get; } = new();

    public HostPlatform Platform => HostPlatform.Windows;

    public bool CanSwitchHdr => true;

    public IReadOnlyList<DisplayInfo> Displays() =>
    [
        new DisplayInfo(
            @"\\.\DISPLAY1",
            "Gallery TV",
            new DisplayBounds(0, 0, 3840, 2160),
            IsMain: true,
            new VideoMode(Sizes[0], RefreshRate.FromLegacyHertz(120)),
            [.. Sizes.SelectMany(size => LegacyRates.Select(rate => new VideoMode(size, RefreshRate.FromLegacyHertz(rate))))],
            HdrState.Off),
    ];

    public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode) => DisplayChangeResult.Failed("the gallery changes nothing");

    public DisplayChangeResult SetHdr(DisplayInfo display, bool on) => DisplayChangeResult.Failed("the gallery changes nothing");
}
#endif
