using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Inspector;

/// <summary>
/// Attached properties that mark a control as a named region for the F1 inspector (ADR-0002 §5d).
/// <c>SectionForm</c>, <c>ItemList</c> and <c>Shell</c> set them from their declarations, so no screen
/// sets them by hand.
/// </summary>
public static class Region
{
    public static readonly AttachedProperty<string?> NameProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Name", typeof(Region));

    public static readonly AttachedProperty<RegionInfo?> InfoProperty =
        AvaloniaProperty.RegisterAttached<Control, RegionInfo?>("Info", typeof(Region));

    public static string? GetName(Control control) => control.GetValue(NameProperty);

    public static void SetName(Control control, string? value) => control.SetValue(NameProperty, value);

    public static RegionInfo? GetInfo(Control control) => control.GetValue(InfoProperty);

    public static void SetInfo(Control control, RegionInfo? value) => control.SetValue(InfoProperty, value);

    /// <summary>Marks a control in one call; <paramref name="info"/> is optional for purely structural regions.</summary>
    public static void Mark(Control control, string name, RegionInfo? info = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(NameProperty, name);
        control.SetValue(InfoProperty, info);
    }
}
