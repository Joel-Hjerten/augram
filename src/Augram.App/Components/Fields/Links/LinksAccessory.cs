using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Components.Fields.Links;

/// <summary>
/// The "Change…" button before a <see cref="Declarations.LinksField"/> (an excluded app's Allowed for, plan 0005; Joel,
/// 2026-10-11: set it from the app's side too). It sits before the links, as the command header's Not in and Also in rows put
/// theirs, so a longer list never moves it.
/// </summary>
public static class LinksAccessory
{
    public const string ChangeLabel = "Change…";

    /// <summary>A <c>toolbar</c> button labelled <see cref="ChangeLabel"/> that calls <paramref name="change"/>.</summary>
    public static Func<Control> Change(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return () =>
        {
            var button = new Button { Content = ChangeLabel, VerticalAlignment = VerticalAlignment.Center };
            button.Classes.Add("toolbar");
            button.Click += (_, _) => change();
            return button;
        };
    }
}
