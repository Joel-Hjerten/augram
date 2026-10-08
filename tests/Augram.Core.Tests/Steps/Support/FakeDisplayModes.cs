using Augram.Core.Abstractions;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>
/// Fake <see cref="IDisplayModes"/>: lists the displays a test sets, records every <see cref="SetMode"/> and
/// <see cref="SetHdr"/> call ("mode \\.\DISPLAY1 1920×1080 at 24 Hz", "hdr \\.\DISPLAY1 on") and answers with a
/// configurable result. Can switch HDR and succeeds by default; it never touches a real display.
/// </summary>
internal sealed class FakeDisplayModes : IDisplayModes
{
    private readonly List<string> _calls = [];

    public HostPlatform Platform { get; set; } = HostPlatform.Windows;

    public bool CanSwitchHdr { get; set; } = true;

    public List<DisplayInfo> Listed { get; } = [];

    public DisplayChangeResult Result { get; set; } = DisplayChangeResult.Ok;

    public IReadOnlyList<string> Calls => _calls;

    public IReadOnlyList<DisplayInfo> Displays() => [.. Listed];

    public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode)
    {
        _calls.Add($"mode {display.Id} {mode}");
        return Result;
    }

    public DisplayChangeResult SetHdr(DisplayInfo display, bool on)
    {
        _calls.Add($"hdr {display.Id} {(on ? "on" : "off")}");
        return Result;
    }

    /// <summary>
    /// A display like Joel's TV: every size in <paramref name="sizes"/> (default 3840×2160, 2560×1440, 1920×1080) at
    /// Windows' legacy rates 23 24 25 29 30 50 59 60 100 119 120 (so 23.976 … 119.88 and the whole rates), currently
    /// 3840×2160 at 120 Hz unless <paramref name="current"/> says otherwise.
    /// </summary>
    public static DisplayInfo Tv(
        string id = @"\\.\DISPLAY1",
        string name = "SONY TV",
        DisplayBounds? bounds = null,
        bool isMain = true,
        VideoMode? current = null,
        HdrState hdr = HdrState.Off,
        IReadOnlyList<DisplayResolution>? sizes = null,
        IReadOnlyList<int>? legacyRates = null)
    {
        sizes ??= [new(3840, 2160), new(2560, 1440), new(1920, 1080)];
        legacyRates ??= [23, 24, 25, 29, 30, 50, 59, 60, 100, 119, 120];
        var modes = sizes.SelectMany(size => legacyRates.Select(rate => new VideoMode(size, RefreshRate.FromLegacyHertz(rate)))).ToList();
        return new DisplayInfo(
            id,
            name,
            bounds ?? new DisplayBounds(0, 0, 3840, 2160),
            isMain,
            current ?? Mode(3840, 2160, 120),
            modes,
            hdr);
    }

    public static VideoMode Mode(int width, int height, double hertz) => new(new DisplayResolution(width, height), RefreshRate.FromHertz(hertz));
}
