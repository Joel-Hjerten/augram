using Augram.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The window the identification form's "Identify window" magnifier last picked (UI-only state of
/// <see cref="AppMatcherEditViewModel"/>, kept apart from the matcher fields so a pick shown here is never an edit the host
/// applies): a <see cref="Summary"/> line, and the window's other properties as text, each with a "Has" flag that shows
/// its row and its Use button only when the window has that property. A newly selected app group or ignored app starts
/// with nothing picked.
/// </summary>
public sealed partial class IdentifiedWindowViewModel : ObservableObject
{
    public const string NothingPicked = "Drag the magnifier onto any window.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathText), nameof(HasPath), nameof(TitleText), nameof(HasTitle), nameof(ClassesText), nameof(HasClasses))]
    public partial WindowIdentity? Window { get; private set; }

    /// <summary>What the last pick did ("chrome.exe · Google Chrome: added chrome.exe to the Windows executables"), or how to start.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = NothingPicked;

    public string PathText => Window?.ProcessPath ?? string.Empty;

    public bool HasPath => !string.IsNullOrEmpty(Window?.ProcessPath);

    public string TitleText => Window?.Title ?? string.Empty;

    public bool HasTitle => !string.IsNullOrEmpty(Window?.Title);

    /// <summary>The class chain as the Window classes field writes it: "Chrome_RenderWidgetHostHWND, Chrome_WidgetWin_1".</summary>
    public string ClassesText => Window is null ? string.Empty : string.Join(", ", Window.ClassChain);

    /// <summary>Windows only in practice: a macOS window has no class chain.</summary>
    public bool HasClasses => Window?.ClassChain.Count > 0;

    public void Show(WindowIdentity window, string summary)
    {
        ArgumentNullException.ThrowIfNull(window);
        Window = window;
        Summary = summary;
    }
}
