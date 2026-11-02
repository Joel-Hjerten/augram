using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Display;

/// <summary>
/// The macOS <see cref="IDisplayModes"/> (learnings 0002 §3). Displays are the active <c>CGDirectDisplayID</c>s with
/// their bounds in global points (the hook's space), "Built-in display" or "Display N" by position in the active list;
/// resolutions are points, the "looks like" size System Settings shows, and rates CoreGraphics' own rounded to three
/// decimals (0, unknown, on displays that report none). A mode is applied through the HiDPI variant when there is one
/// (<see cref="MacModePick"/>) and stored permanently, like System Settings. HDR is read and switched through SkyLight's
/// private calls, the switch behind System Settings › Displays › High Dynamic Range (<see cref="MacNative.SkyLightHdr"/>,
/// plan 0014); a macOS without them reads every display unsupported and <see cref="CanSwitchHdr"/> false.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacDisplayModes : IDisplayModes
{
    public HostPlatform Platform => HostPlatform.MacOS;

    /// <summary>How long <see cref="SetHdr"/> waits for the display to report the new state (it re-syncs in about 0.65 s).</summary>
    private static readonly TimeSpan HdrSettleTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan HdrPollInterval = TimeSpan.FromMilliseconds(50);

    public bool CanSwitchHdr => MacNative.SkyLightHdr.IsAvailable;

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
                MacModePick.Offered(reading.Modes),
                reading.Hdr));
        }

        return displays;
    }

    public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (!TryParseId(display, out var id)
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

    /// <summary>Asks SkyLight for HDR on or off, then waits up to <see cref="HdrSettleTimeout"/> for the display to report it, so the next step or a quick second toggle sees the new state.</summary>
    public DisplayChangeResult SetHdr(DisplayInfo display, bool on)
    {
        ArgumentNullException.ThrowIfNull(display);
        var wanted = on ? HdrState.On : HdrState.Off;
        if (!MacNative.SkyLightHdr.IsAvailable)
        {
            return DisplayChangeResult.Failed("switching HDR is not supported on MacOS");
        }

        if (!TryParseId(display, out var id))
        {
            return DisplayChangeResult.Failed($"{display.Name} is gone");
        }

        var error = MacNative.SkyLightHdr.SetEnabled(id, on);
        if (error != MacNative.CGErrorSuccess)
        {
            return DisplayChangeResult.Failed($"HDR {(on ? "on" : "off")} on {display.Name}: SLSDisplaySetHDRModeEnabled failed ({error})");
        }

        var waited = Stopwatch.StartNew();
        while (MacDisplayList.ReadHdr(id) != wanted)
        {
            if (waited.Elapsed >= HdrSettleTimeout)
            {
                return DisplayChangeResult.Failed($"HDR did not turn {(on ? "on" : "off")} on {display.Name} within {HdrSettleTimeout.TotalSeconds:0} s");
            }

            Thread.Sleep(HdrPollInterval);
        }

        return DisplayChangeResult.Ok;
    }

    private static bool TryParseId(DisplayInfo display, out uint id)
        => uint.TryParse(display.Id, NumberStyles.None, CultureInfo.InvariantCulture, out id);
}
