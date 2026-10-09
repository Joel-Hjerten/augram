using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.Core.Steps.OpenApp;

/// <summary>
/// Brings an app to the front, or starts it when it is not running (Joel, 2026-10-09: "open or focus any app"; SP.net users
/// script this as activate-or-run). <see cref="IsAugram"/> opens Augram's own window instead (as a double click on its tray
/// or menu-bar icon does). Otherwise the app is named per platform, like an app group's executables:
/// <see cref="WindowsApp"/> ("chrome.exe") and <see cref="MacApp"/> ("Google Chrome"); a platform left empty uses the
/// known-app guess from the other (<see cref="KnownApps"/>), so the step needs no own version (F8).
/// </summary>
public sealed record OpenAppStep(bool IsAugram, string WindowsApp, string MacApp) : IStep
{
    public const string AugramText = "Open Augram";

    public IStepType Type => OpenAppStepType.Instance;

    public bool IsSet => IsAugram || WindowsApp.Trim().Length > 0 || MacApp.Trim().Length > 0;

    public string Summary => SummaryOn(OpenAppStepType.CurrentPlatform);

    public string SummaryOn(HostPlatform platform)
        => IsAugram ? AugramText
            : AppFor(platform) is { } app ? $"Open {app}"
            : IsSet ? $"Open app (none for {(platform == HostPlatform.MacOS ? "macOS" : "Windows")})"
            : "Open app (no app set)";

    /// <summary>The app to open on <paramref name="platform"/>: its own name, else the guess from the other platform's; null when neither gives one.</summary>
    public string? AppFor(HostPlatform platform)
    {
        var (own, other) = platform == HostPlatform.MacOS ? (MacApp, WindowsApp) : (WindowsApp, MacApp);
        if (own.Trim().Length > 0)
        {
            return own.Trim();
        }

        return other.Trim().Length > 0 && KnownApps.Guess([other.Trim()], platform) is [var guess, ..] ? guess : null;
    }
}
