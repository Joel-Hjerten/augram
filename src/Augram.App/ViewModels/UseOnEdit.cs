using System.Runtime.CompilerServices;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The "Use on ☑ Windows ☑ macOS" part of an edit form (F8): an app group's, a hold remap's and a category's. Two boxes, both
/// ticked by default; <see cref="Value"/> is them as a <see cref="PlatformSet"/>, <see cref="SyncFrom"/> takes a stored one and
/// <see cref="Declare"/> is the row, with the help the form passes. The form raises this one's changes as its own, so a host
/// listens in one place (<see cref="UseOnWindows"/>, <see cref="UseOnMac"/>). Nothing is validated here: unticking both is the
/// store's to refuse.
/// </summary>
public sealed partial class UseOnEdit : ObservableObject
{
    [ObservableProperty]
    public partial bool UseOnWindows { get; set; } = true;

    [ObservableProperty]
    public partial bool UseOnMac { get; set; } = true;

    /// <summary>The ticked platforms.</summary>
    public PlatformSet Value => PlatformSet.None.With(HostPlatform.Windows, UseOnWindows).With(HostPlatform.MacOS, UseOnMac);

    /// <summary>True for the two properties this raises.</summary>
    public static bool IsUseOn(string? propertyName) => propertyName is nameof(UseOnWindows) or nameof(UseOnMac);

    /// <summary>Takes the stored platforms; a box that already holds the value raises nothing.</summary>
    public void SyncFrom(PlatformSet useOn)
    {
        UseOnWindows = useOn.Includes(HostPlatform.Windows);
        UseOnMac = useOn.Includes(HostPlatform.MacOS);
    }

    /// <summary>The "Use on" row: a box per platform, by its display name; F1 points at the form that declares it.</summary>
    public TogglesField Declare(string help, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => new(
        "Use on",
        [
            new ToggleOption(HostPlatform.Windows.DisplayName(), new DelegateBinding<bool>(() => UseOnWindows, value => UseOnWindows = value, this)),
            new ToggleOption(HostPlatform.MacOS.DisplayName(), new DelegateBinding<bool>(() => UseOnMac, value => UseOnMac = value, this)),
        ],
        help,
        file,
        line);
}
