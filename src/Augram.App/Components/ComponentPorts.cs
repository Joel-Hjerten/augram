using Augram.App.Components.ButtonDetect;
using Augram.App.Components.HotkeyCapture;
using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Components;

/// <summary>
/// The ports a component reaches for although it is built without services: step forms and finders are made by assembly scan
/// (<c>StepFormRegistry</c>, the field renderers), so they look these up as one application resource under
/// <see cref="ResourceKey"/>, which the composition root publishes once the engine has started (<c>Hosting/Modules/FormServices</c>).
/// Each component's own explicit property (tests, gallery) wins over it. The captures are null under <c>--no-engine</c>: the
/// hotkey field then takes its window's keys and the button detector its window's presses. A test may put its own instance on
/// a window's resources.
/// </summary>
public sealed record ComponentPorts
{
    public const string ResourceKey = "Augram.ComponentPorts";

    /// <summary>The display modes, for the Display mode step form.</summary>
    public IDisplayModes? DisplayModes { get; init; }

    /// <summary>The window system, for the window finder's magnifiers.</summary>
    public IWindowSystem? WindowSystem { get; init; }

    /// <summary>The engine's key capture, for every hotkey field (F5); null without the engine.</summary>
    public IKeyCapture? KeyCapture { get; init; }

    /// <summary>The engine's button detection, for a hold remap command's Add button… (plan 0002 step 4); null without the engine.</summary>
    public IButtonCapture? ButtonCapture { get; init; }

    /// <summary>The ports in reach of <paramref name="control"/>: its own or an ancestor's resources, up to the application's.</summary>
    public static ComponentPorts? Near(IResourceHost control) =>
        control.TryFindResource(ResourceKey, out var found) ? found as ComponentPorts : null;

    /// <summary>The application's, for a form that is not in a window yet.</summary>
    public static ComponentPorts? OfApplication() =>
        Application.Current?.Resources.TryGetValue(ResourceKey, out var found) == true ? found as ComponentPorts : null;
}
