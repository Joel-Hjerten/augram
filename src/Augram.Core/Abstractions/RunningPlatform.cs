namespace Augram.Core.Abstractions;

/// <summary>
/// The one answer to "which platform am I on". The operating system's answer until someone sets it; the App's composition
/// root sets it once at startup (<see cref="UseThisMachine"/>), and a test may set <see cref="Current"/> to read as the other
/// platform and <see cref="Reset"/> it afterwards. Core and App read <see cref="Current"/>; the Engine learns its platform
/// from <c>EnginePorts.Platform</c>. Code that needs the OS for a library's or the toolkit's own sake (SharpHook's two quirks,
/// window chrome) asks <see cref="OperatingSystem"/> itself.
/// </summary>
public static class RunningPlatform
{
    private static HostPlatform? _set;

    /// <summary>The platform this process runs as: the one set, else the operating system's answer.</summary>
    public static HostPlatform Current
    {
        get => _set ?? OperatingSystemAnswer;
        set => _set = value;
    }

    /// <summary>The platform set, null until something sets it; <see cref="Steps.Hotkey.HotkeyText.Names"/> reads Windows names until then.</summary>
    internal static HostPlatform? Chosen => _set;

    /// <summary>For the composition root, once at startup: from here on the process reads as the system it runs on, key names included.</summary>
    public static void UseThisMachine() => _set = OperatingSystemAnswer;

    /// <summary>For a test that set <see cref="Current"/>: back to the state before anything set it.</summary>
    public static void Reset() => _set = null;

    private static HostPlatform OperatingSystemAnswer => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;
}
