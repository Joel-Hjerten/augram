using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.Parts;

/// <summary>
/// Wiring a component's named template parts (<c>PART_*</c>, the contract with <c>Themes/</c>) in <c>OnApplyTemplate</c>. A
/// theme may leave a part out, so every lookup tolerates a missing one.
/// </summary>
internal static class TemplateParts
{
    /// <summary>Runs <paramref name="action"/> on every click of the template's button <paramref name="part"/>; returns the button, or null when the template has none.</summary>
    public static Button? OnClick(this TemplateAppliedEventArgs e, string part, Action action)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(action);
        var button = e.NameScope.Find<Button>(part);
        if (button is not null)
        {
            button.Click += (_, _) => action();
        }

        return button;
    }
}
