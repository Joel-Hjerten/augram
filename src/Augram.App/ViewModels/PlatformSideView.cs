using Augram.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// Which platform's side a per-platform view shows, the one home of that view state: the app identification form's
/// "Fields for" switch (Joel, 2026-10-09: one layout, flipped between Windows and macOS) and a command's "Steps for"
/// switch (plan 0010 decision 1). UI state only, kept apart from what it shows, so a flip is never an edit. It opens on
/// <see cref="Here"/>, the platform Augram runs on and the one side edited here (plan 0010 decisions 2, 6 and 7); the
/// other side is shown read-only, with <see cref="EditNote"/>.
/// </summary>
public sealed partial class PlatformSideView : ObservableObject
{
    public PlatformSideView(HostPlatform here)
    {
        Here = here;
        Shown = here;
    }

    /// <summary>The platform Augram runs on: the side that opens, and the only one edited here.</summary>
    public HostPlatform Here { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWindows), nameof(IsHere))]
    public partial HostPlatform Shown { get; set; }

    public bool ShowsWindows => Shown == HostPlatform.Windows;

    /// <summary>The side shown is this machine's, so it is edited; false on the other side, which is read-only.</summary>
    public bool IsHere => Shown == Here;

    /// <summary>The other side's note (plan 0010, settled wording): "Edit on Windows".</summary>
    public static string EditNote(HostPlatform side) => $"Edit on {side.DisplayName()}";

    /// <summary>Back to this machine's side (another command selected).</summary>
    public void Reset() => Shown = Here;
}
