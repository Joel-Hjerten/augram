using Augram.Platform.Windows.Display;

namespace Augram.Platform.Windows.Tests.Display;

/// <summary>
/// Scripted <see cref="IWin32Displays"/>: devices, settings and CCD targets set by the test; <see cref="Change"/> and
/// <see cref="SetHdr"/> are recorded ("test \\.\DISPLAY1 1920x1080@119", "apply …", "hdr-state … on") and answered from
/// queues. Nothing here reaches a real display.
/// </summary>
internal sealed class FakeWin32Displays : IWin32Displays
{
    public List<Win32DisplayDevice> DeviceList { get; } = [];

    public Dictionary<string, Win32DisplaySetting> CurrentSettings { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<Win32DisplaySetting>> AllSettings { get; } = new(StringComparer.Ordinal);

    public List<Win32DisplayTarget> TargetList { get; } = [];

    /// <summary>Answers for <see cref="Change"/>, in call order; success once it runs out.</summary>
    public Queue<int> ChangeResults { get; } = new();

    public int HdrResult { get; set; }

    public List<string> Calls { get; } = [];

    public List<Win32DisplaySetting> Applied { get; } = [];

    public IReadOnlyList<Win32DisplayDevice> Devices() => DeviceList;

    public Win32DisplaySetting? Current(string device) => CurrentSettings.GetValueOrDefault(device);

    public IReadOnlyList<Win32DisplaySetting> Settings(string device) => AllSettings.GetValueOrDefault(device) ?? [];

    public int Change(string device, Win32DisplaySetting setting, bool test)
    {
        Calls.Add($"{(test ? "test" : "apply")} {device} {setting.Width}x{setting.Height}@{setting.Frequency}");
        Applied.Add(setting);
        return ChangeResults.Count > 0 ? ChangeResults.Dequeue() : 0;
    }

    public IReadOnlyList<Win32DisplayTarget> Targets() => TargetList;

    public int SetHdr(Win32DisplayTarget target, bool on, bool hdrState)
    {
        Calls.Add($"{(hdrState ? "hdr-state" : "advanced-color")} {target.SourceName} {(on ? "on" : "off")}");
        return HdrResult;
    }

    /// <summary>A desktop-attached device with every size at Joel's TV's legacy rates (both fixed-output variants and an interlaced 1080i among them).</summary>
    public FakeWin32Displays WithTv(string name = @"\\.\DISPLAY1", bool primary = true, int x = 0, int currentFrequency = 120, uint? info2 = 0x51, uint? info = 0x1)
    {
        DeviceList.Add(new Win32DisplayDevice(name, 0x1 | (primary ? 0x4u : 0)));
        CurrentSettings[name] = new Win32DisplaySetting(3840, 2160, currentFrequency, X: x);
        var settings = new List<Win32DisplaySetting>();
        foreach (var (width, height) in new[] { (3840, 2160), (1920, 1080) })
        {
            foreach (var rate in new[] { 23, 24, 25, 29, 30, 50, 59, 60, 100, 119, 120 })
            {
                settings.Add(new Win32DisplaySetting(width, height, rate, HasFixedOutput: true, FixedOutput: 2));
                settings.Add(new Win32DisplaySetting(width, height, rate, HasFixedOutput: true, FixedOutput: 0));
                settings.Add(new Win32DisplaySetting(width, height, rate, BitsPerPixel: 16));
            }
        }

        settings.Add(new Win32DisplaySetting(1920, 1080, 60, Interlaced: true));
        settings.Add(new Win32DisplaySetting(1920, 1080, 25, Interlaced: true));
        AllSettings[name] = settings;
        TargetList.Add(new Win32DisplayTarget(name, "SONY TV  *30", 0x1_0000_0002, 4352, info2, info));
        return this;
    }
}
