using Augram.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// Which platform's fields the identification form shows (Joel, 2026-10-09: one layout, flipped between Windows and macOS
/// by a switch at the top). UI state only, kept apart from <see cref="AppMatcherEditViewModel"/> so a flip is never an edit
/// the host applies. Opens on this machine's platform.
/// </summary>
public sealed partial class AppMatcherFormView : ObservableObject
{
    public AppMatcherFormView(HostPlatform shown)
    {
        Shown = shown;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWindows))]
    public partial HostPlatform Shown { get; set; }

    public bool ShowsWindows => Shown == HostPlatform.Windows;
}
