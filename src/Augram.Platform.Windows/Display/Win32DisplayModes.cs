using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;

namespace Augram.Platform.Windows.Display;

/// <summary>
/// The Windows <see cref="IDisplayModes"/> (learnings 0002 §2). Displays are the devices attached to the desktop
/// (mirroring drivers left out), named by their monitor's CCD friendly name, placed by their current settings in
/// physical pixels. Modes are the progressive settings the monitor lists, with Windows' whole hertz turned into the
/// exact rate (<see cref="RefreshRate.FromLegacyHertz"/>) and each mode once whatever its colour depth or fixed-output
/// variants. <see cref="SetMode"/> sends the listed setting that carries the mode (current colour depth and the default
/// fixed output first), tests it with <c>CDS_TEST</c> and only then applies and stores it (<c>CDS_UPDATEREGISTRY</c>,
/// like Settings). HDR goes through the 24H2 HDR state when Windows has it, else the older advanced colour state.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32DisplayModes : IDisplayModes
{
    private readonly IWin32Displays _native;

    public Win32DisplayModes()
        : this(new Win32Displays())
    {
    }

    internal Win32DisplayModes(IWin32Displays native)
    {
        _native = native;
    }

    public HostPlatform Platform => HostPlatform.Windows;

    public bool CanSwitchHdr => true;

    public IReadOnlyList<DisplayInfo> Displays()
    {
        var targets = _native.Targets();
        var displays = new List<DisplayInfo>();
        foreach (var device in _native.Devices())
        {
            if ((device.StateFlags & NativeMethods.DisplayDeviceAttachedToDesktop) == 0
                || (device.StateFlags & NativeMethods.DisplayDeviceMirroringDriver) != 0
                || _native.Current(device.Name) is not { } current)
            {
                continue;
            }

            var target = TargetOf(targets, device.Name);
            var modes = _native.Settings(device.Name).Where(setting => !setting.Interlaced).Select(ToMode).Distinct().ToList();
            displays.Add(new DisplayInfo(
                device.Name,
                NameOf(target, device.Name),
                new DisplayBounds(current.X, current.Y, current.Width, current.Height),
                (device.StateFlags & NativeMethods.DisplayDevicePrimaryDevice) != 0,
                ToMode(current),
                modes,
                Win32HdrInfo.From(target).State));
        }

        return displays;
    }

    public DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode)
    {
        ArgumentNullException.ThrowIfNull(display);
        var currentDepth = _native.Current(display.Id)?.BitsPerPixel;
        var setting = _native.Settings(display.Id)
            .Where(candidate => !candidate.Interlaced && ToMode(candidate) == mode)
            .OrderBy(candidate => candidate.BitsPerPixel == currentDepth ? 0 : 1)
            .ThenByDescending(candidate => candidate.BitsPerPixel)
            .ThenBy(candidate => candidate.HasFixedOutput ? candidate.FixedOutput : 0)
            .FirstOrDefault();
        if (setting is null)
        {
            return DisplayChangeResult.Failed($"{display.Name} no longer offers {mode}");
        }

        var tested = _native.Change(display.Id, setting, test: true);
        if (tested != DisplayChangeCode.Successful)
        {
            return DisplayChangeResult.Failed($"Windows refused {mode} on {display.Name} ({DisplayChangeCode.Describe(tested)})");
        }

        var applied = _native.Change(display.Id, setting, test: false);
        return applied == DisplayChangeCode.Successful
            ? DisplayChangeResult.Ok
            : DisplayChangeResult.Failed($"{mode} on {display.Name} did not apply ({DisplayChangeCode.Describe(applied)})");
    }

    public DisplayChangeResult SetHdr(DisplayInfo display, bool on)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (TargetOf(_native.Targets(), display.Id) is not { } target)
        {
            return DisplayChangeResult.Failed($"{display.Name} has no active display path");
        }

        var error = _native.SetHdr(target, on, Win32HdrInfo.From(target).UsesHdrState);
        return error == 0
            ? DisplayChangeResult.Ok
            : DisplayChangeResult.Failed($"DisplayConfigSetDeviceInfo failed ({error})");
    }

    private static VideoMode ToMode(Win32DisplaySetting setting)
        => new(new DisplayResolution(setting.Width, setting.Height), RefreshRate.FromLegacyHertz(setting.Frequency));

    private static Win32DisplayTarget? TargetOf(IReadOnlyList<Win32DisplayTarget> targets, string device)
        => targets.FirstOrDefault(target => string.Equals(target.SourceName, device, StringComparison.OrdinalIgnoreCase));

    /// <summary>The monitor's friendly name with its padding squeezed ("SONY TV  *30" → "SONY TV *30"), else the device name without "\\.\".</summary>
    private static string NameOf(Win32DisplayTarget? target, string device)
    {
        var friendly = string.Join(' ', (target?.FriendlyName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return friendly.Length > 0 ? friendly : device.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
    }
}
