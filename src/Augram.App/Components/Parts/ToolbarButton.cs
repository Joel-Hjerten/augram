using Avalonia.Controls;

namespace Augram.App.Components.Parts;

/// <summary>
/// The one way code builds a button: the theme's <c>toolbar</c> class (Theme rule: the class says how it looks) and an action
/// on every click. A caller sets anything else (a name, a tooltip, <c>IsDefault</c>) on the button it gets back.
/// </summary>
internal static class ToolbarButton
{
    public static Button Create(object? content, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var button = new Button { Content = content };
        button.Classes.Add("toolbar");
        button.Click += (_, _) => action();
        return button;
    }
}
