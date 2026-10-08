namespace Augram.Platform.Windows.Display;

/// <summary>
/// The Win32 display calls <see cref="Win32DisplayModes"/> needs, as thin as the API (the real one is
/// <c>Interop/Win32Displays</c>). A seam so the adapter's rules (which devices count, the legacy rate rule, which
/// setting variant to apply, test before apply, which HDR call to use) are tested against scripted displays; no test
/// touches the real ones.
/// </summary>
internal interface IWin32Displays
{
    /// <summary>Every display device <c>EnumDisplayDevices</c> lists, attached or not.</summary>
    IReadOnlyList<Win32DisplayDevice> Devices();

    /// <summary>The device's current settings (<c>ENUM_CURRENT_SETTINGS</c>, position included); null when unreadable.</summary>
    Win32DisplaySetting? Current(string device);

    /// <summary>Every setting the device lists for its monitor (<c>EnumDisplaySettingsEx</c> from index 0).</summary>
    IReadOnlyList<Win32DisplaySetting> Settings(string device);

    /// <summary><c>ChangeDisplaySettingsEx</c> with <c>CDS_TEST</c> when <paramref name="test"/>, else <c>CDS_UPDATEREGISTRY</c>; the <c>DISP_CHANGE_*</c> code.</summary>
    int Change(string device, Win32DisplaySetting setting, bool test);

    /// <summary>The active CCD paths with their source name, monitor name and raw advanced colour values.</summary>
    IReadOnlyList<Win32DisplayTarget> Targets();

    /// <summary><c>DisplayConfigSetDeviceInfo</c> with <c>SET_HDR_STATE</c> when <paramref name="hdrState"/>, else <c>SET_ADVANCED_COLOR_STATE</c>; the Win32 error, 0 on success.</summary>
    int SetHdr(Win32DisplayTarget target, bool on, bool hdrState);
}
