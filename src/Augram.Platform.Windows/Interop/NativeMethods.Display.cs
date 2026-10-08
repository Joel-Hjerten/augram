using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Augram.Platform.Windows.Interop;

/// <summary>
/// The display part of the P/Invoke surface (learnings 0002 §2): the legacy display settings API that lists and sets
/// modes, and the CCD calls that name the monitor and read and switch HDR. No logic; <see cref="Win32Displays"/> wraps it.
/// Text buffers are inline arrays so every struct stays blittable under disabled runtime marshalling.
/// </summary>
internal static partial class NativeMethods
{
    public const uint EnumCurrentSettings = 0xFFFFFFFF;
    public const uint DisplayDeviceAttachedToDesktop = 0x1;
    public const uint DisplayDevicePrimaryDevice = 0x4;
    public const uint DisplayDeviceMirroringDriver = 0x8;
    public const uint DmBitsPerPel = 0x00040000;
    public const uint DmPelsWidth = 0x00080000;
    public const uint DmPelsHeight = 0x00100000;
    public const uint DmDisplayFlags = 0x00200000;
    public const uint DmDisplayFrequency = 0x00400000;
    public const uint DmDisplayFixedOutput = 0x20000000;
    public const uint DmInterlaced = 0x2;
    public const uint CdsUpdateRegistry = 0x1;
    public const uint CdsTest = 0x2;
    public const uint QdcOnlyActivePaths = 0x2;
    public const int ErrorInsufficientBuffer = 122;
    public const uint DeviceInfoGetSourceName = 1;
    public const uint DeviceInfoGetTargetName = 2;
    public const uint DeviceInfoGetAdvancedColorInfo = 9;
    public const uint DeviceInfoSetAdvancedColorState = 10;
    public const uint DeviceInfoGetAdvancedColorInfo2 = 15;
    public const uint DeviceInfoSetHdrState = 16;

    [InlineArray(32)]
    public struct Chars32
    {
        private char _first;
    }

    [InlineArray(64)]
    public struct Chars64
    {
        private char _first;
    }

    [InlineArray(128)]
    public struct Chars128
    {
        private char _first;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayDevice
    {
        public uint Size;
        public Chars32 DeviceName;
        public Chars128 DeviceString;
        public uint StateFlags;
        public Chars128 DeviceId;
        public Chars128 DeviceKey;
    }

    /// <summary><c>DEVMODEW</c> with the display members of its unions (220 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DevMode
    {
        public Chars32 DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        public Chars32 FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint IcmMethod;
        public uint IcmIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DisplayConfigRational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    /// <summary><c>DISPLAYCONFIG_PATH_INFO</c> (72 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    /// <summary><c>DISPLAYCONFIG_MODE_INFO</c>: only its size matters here (64 bytes); the union is never read.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct DisplayConfigModeInfo
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    /// <summary><c>DISPLAYCONFIG_SOURCE_DEVICE_NAME</c> (84 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public Chars32 ViewGdiDeviceName;
    }

    /// <summary><c>DISPLAYCONFIG_TARGET_DEVICE_NAME</c> (420 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        public Chars64 MonitorFriendlyDeviceName;
        public Chars128 MonitorDevicePath;
    }

    /// <summary>
    /// <c>DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO</c> (32 bytes, <see cref="DeviceInfoGetAdvancedColorInfo"/>) and, with
    /// <see cref="ActiveColorMode"/>, <c>DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2</c> (36 bytes, Windows 11 24H2); the header's
    /// size says which one is asked for.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigAdvancedColorInfo
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;
        public uint ActiveColorMode;
    }

    /// <summary><c>DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE</c> and <c>DISPLAYCONFIG_SET_HDR_STATE</c>: the header and one bit (24 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayConfigSetState
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
    }

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice displayDevice, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsExW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplaySettingsEx(string device, uint modeNumber, ref DevMode devMode, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int ChangeDisplaySettingsEx(string device, ref DevMode devMode, nint window, uint flags, nint parameter);

    [LibraryImport("user32.dll")]
    public static partial int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [LibraryImport("user32.dll")]
    public static partial int QueryDisplayConfig(uint flags, ref uint pathCount, Span<DisplayConfigPathInfo> paths, ref uint modeCount, Span<DisplayConfigModeInfo> modes, nint currentTopologyId);

    [LibraryImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static partial int DisplayConfigGetSourceName(ref DisplayConfigSourceDeviceName request);

    [LibraryImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static partial int DisplayConfigGetTargetName(ref DisplayConfigTargetDeviceName request);

    [LibraryImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    public static partial int DisplayConfigGetAdvancedColorInfo(ref DisplayConfigAdvancedColorInfo request);

    [LibraryImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")]
    public static partial int DisplayConfigSetDeviceInfo(ref DisplayConfigSetState request);
}
