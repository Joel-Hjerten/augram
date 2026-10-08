using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Augram.Platform.Windows.Display;

namespace Augram.Platform.Windows.Interop;

/// <summary>
/// The real <see cref="IWin32Displays"/>: thin wrappers over <see cref="NativeMethods"/>' display calls, no decisions.
/// Reads are cheap (tens of milliseconds for a full mode list); <see cref="Change"/> and <see cref="SetHdr"/> change the
/// machine's display and are only ever called by <see cref="Win32DisplayModes"/> on the command executor thread.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32Displays : IWin32Displays
{
    private static readonly uint DevModeSize = (uint)Unsafe.SizeOf<NativeMethods.DevMode>();

    public IReadOnlyList<Win32DisplayDevice> Devices()
    {
        var devices = new List<Win32DisplayDevice>();
        var device = new NativeMethods.DisplayDevice { Size = (uint)Unsafe.SizeOf<NativeMethods.DisplayDevice>() };
        for (uint index = 0; NativeMethods.EnumDisplayDevices(null, index, ref device, 0); index++)
        {
            devices.Add(new Win32DisplayDevice(Text(device.DeviceName), device.StateFlags));
            device = new NativeMethods.DisplayDevice { Size = (uint)Unsafe.SizeOf<NativeMethods.DisplayDevice>() };
        }

        return devices;
    }

    public Win32DisplaySetting? Current(string device)
    {
        var mode = new NativeMethods.DevMode { Size = (ushort)DevModeSize };
        return NativeMethods.EnumDisplaySettingsEx(device, NativeMethods.EnumCurrentSettings, ref mode, 0) ? ToSetting(mode) : null;
    }

    public IReadOnlyList<Win32DisplaySetting> Settings(string device)
    {
        var settings = new List<Win32DisplaySetting>();
        var mode = new NativeMethods.DevMode { Size = (ushort)DevModeSize };
        for (uint index = 0; NativeMethods.EnumDisplaySettingsEx(device, index, ref mode, 0); index++)
        {
            settings.Add(ToSetting(mode));
            mode = new NativeMethods.DevMode { Size = (ushort)DevModeSize };
        }

        return settings;
    }

    public int Change(string device, Win32DisplaySetting setting, bool test)
    {
        var fields = NativeMethods.DmPelsWidth | NativeMethods.DmPelsHeight | NativeMethods.DmDisplayFrequency
            | NativeMethods.DmBitsPerPel | NativeMethods.DmDisplayFlags;
        if (setting.HasFixedOutput)
        {
            fields |= NativeMethods.DmDisplayFixedOutput;
        }

        var mode = new NativeMethods.DevMode
        {
            Size = (ushort)DevModeSize,
            Fields = fields,
            PelsWidth = (uint)setting.Width,
            PelsHeight = (uint)setting.Height,
            DisplayFrequency = (uint)setting.Frequency,
            BitsPerPel = (uint)setting.BitsPerPixel,
            DisplayFlags = setting.Interlaced ? NativeMethods.DmInterlaced : 0,
            DisplayFixedOutput = (uint)setting.FixedOutput,
        };
        return NativeMethods.ChangeDisplaySettingsEx(device, ref mode, 0, test ? NativeMethods.CdsTest : NativeMethods.CdsUpdateRegistry, 0);
    }

    public IReadOnlyList<Win32DisplayTarget> Targets()
    {
        var paths = ActivePaths();
        var targets = new List<Win32DisplayTarget>(paths.Length);
        foreach (var path in paths)
        {
            var source = new NativeMethods.DisplayConfigSourceDeviceName
            {
                Header = Header(NativeMethods.DeviceInfoGetSourceName, Unsafe.SizeOf<NativeMethods.DisplayConfigSourceDeviceName>(), path.SourceInfo.AdapterId, path.SourceInfo.Id),
            };
            if (NativeMethods.DisplayConfigGetSourceName(ref source) != 0)
            {
                continue;
            }

            var adapter = path.TargetInfo.AdapterId;
            var name = new NativeMethods.DisplayConfigTargetDeviceName
            {
                Header = Header(NativeMethods.DeviceInfoGetTargetName, Unsafe.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>(), adapter, path.TargetInfo.Id),
            };
            var friendly = NativeMethods.DisplayConfigGetTargetName(ref name) == 0 ? Text(name.MonitorFriendlyDeviceName) : string.Empty;
            targets.Add(new Win32DisplayTarget(
                Text(source.ViewGdiDeviceName),
                friendly,
                ((long)adapter.HighPart << 32) | adapter.LowPart,
                path.TargetInfo.Id,
                AdvancedColor(NativeMethods.DeviceInfoGetAdvancedColorInfo2, 36, adapter, path.TargetInfo.Id),
                AdvancedColor(NativeMethods.DeviceInfoGetAdvancedColorInfo, 32, adapter, path.TargetInfo.Id)));
        }

        return targets;
    }

    public int SetHdr(Win32DisplayTarget target, bool on, bool hdrState)
    {
        var adapter = new NativeMethods.Luid { LowPart = (uint)(target.AdapterId & 0xFFFFFFFF), HighPart = (int)(target.AdapterId >> 32) };
        var request = new NativeMethods.DisplayConfigSetState
        {
            Header = Header(
                hdrState ? NativeMethods.DeviceInfoSetHdrState : NativeMethods.DeviceInfoSetAdvancedColorState,
                Unsafe.SizeOf<NativeMethods.DisplayConfigSetState>(),
                adapter,
                target.TargetId),
            Value = on ? 1u : 0u,
        };
        return NativeMethods.DisplayConfigSetDeviceInfo(ref request);
    }

    private static NativeMethods.DisplayConfigPathInfo[] ActivePaths()
    {
        // The topology can change between the two calls; the second then reports a short buffer and is retried.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0)
            {
                return [];
            }

            var paths = new NativeMethods.DisplayConfigPathInfo[pathCount];
            var modes = new NativeMethods.DisplayConfigModeInfo[modeCount];
            var result = NativeMethods.QueryDisplayConfig(NativeMethods.QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, 0);
            if (result == 0)
            {
                return paths[..(int)pathCount];
            }

            if (result != NativeMethods.ErrorInsufficientBuffer)
            {
                return [];
            }
        }

        return [];
    }

    private static uint? AdvancedColor(uint type, int size, NativeMethods.Luid adapter, uint targetId)
    {
        var info = new NativeMethods.DisplayConfigAdvancedColorInfo { Header = Header(type, size, adapter, targetId) };
        return NativeMethods.DisplayConfigGetAdvancedColorInfo(ref info) == 0 ? info.Value : null;
    }

    private static NativeMethods.DisplayConfigDeviceInfoHeader Header(uint type, int size, NativeMethods.Luid adapter, uint id)
        => new() { Type = type, Size = (uint)size, AdapterId = adapter, Id = id };

    private static Win32DisplaySetting ToSetting(NativeMethods.DevMode mode) => new(
        (int)mode.PelsWidth,
        (int)mode.PelsHeight,
        (int)mode.DisplayFrequency,
        (int)mode.BitsPerPel,
        (mode.DisplayFlags & NativeMethods.DmInterlaced) != 0,
        (int)mode.DisplayFixedOutput,
        (mode.Fields & NativeMethods.DmDisplayFixedOutput) != 0,
        mode.PositionX,
        mode.PositionY);

    private static string Text(ReadOnlySpan<char> chars)
    {
        var end = chars.IndexOf('\0');
        return new string(end < 0 ? chars : chars[..end]);
    }
}
