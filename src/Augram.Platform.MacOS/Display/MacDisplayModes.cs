using System.Globalization;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Display;

/// <summary>
/// The macOS <see cref="IDisplayModes"/> (learnings 0002 §3). Displays are the active <c>CGDirectDisplayID</c>s with
/// their bounds in global points (the hook's space), "Built-in display" or "Display N" by position in the active list;
/// resolutions are points, the "looks like" size System Settings shows, and rates CoreGraphics' own rounded to three
/// decimals (0, unknown, on displays that report none). A mode is applied through the HiDPI variant when there is one
/// (<see cref="MacModePick"/>) and stored permanently, like System Settings. HDR cannot be switched: macOS has no public
/// API for it, so <see cref="CanSwitchHdr"/> is false and every display reads <see cref="HdrState.Unsupported"/>.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacDisplayModes : IDisplayModes
{
    public HostPlatform Platform => HostPlatform.MacOS;

    public bool CanSwitchHdr => false;

    public IReadOnlyList<DisplayInfo> Displays()
    {
        var readings = MacDisplayList.Read();
        var displays = new List<DisplayInfo>(readings.Count);
        for (var index = 0; index < readings.Count; index++)
        {
            var reading = readings[index];
            var bounds = new DisplayBounds(
                (int)Math.Round(reading.Bounds.X),
                (int)Math.Round(reading.Bounds.Y),
                (int)Math.Round(reading.Bounds.Width),
                (int)Math.Round(reading.Bounds.Height));
            var current = reading.Current is { } mode
                ? MacModePick.ToMode(mode)
                : new VideoMode(new DisplayResolution(bounds.Width, bounds.Height), RefreshRate.Unknown);
            displays.Add(new DisplayInfo(
                reading.Id.ToString(CultureInfo.InvariantCulture),
                reading.IsBuiltIn ? "Built-in display" : $"Display {index + 1}",
                bounds,
                reading.IsMain,
                current,
                MacModePick.Offered(reading.Modes)));
        }

        return displays;
    }

    public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (!uint.TryParse(display.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || MacDisplayList.Read().FirstOrDefault(reading => reading.Id == id) is not { } reading)
        {
            return DisplayChangeResult.Failed($"{display.Name} is gone");
        }

        if (MacModePick.Pick(reading.Modes, mode) is not { } native)
        {
            return DisplayChangeResult.Failed($"{display.Name} no longer offers {mode}");
        }

        var error = MacDisplayList.Apply(id, native);
        return error is null ? DisplayChangeResult.Ok : DisplayChangeResult.Failed($"{mode} on {display.Name}: {error}");
    }

    public DisplayChangeResult SetHdr(DisplayInfo display, bool on)
        => DisplayChangeResult.Failed("switching HDR is not supported on MacOS");
}
