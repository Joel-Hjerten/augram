using Augram.Core.Abstractions;
using Avalonia.Controls;

namespace Augram.App.Components.WindowFinder;

/// <summary>
/// The controls a declared form puts before a field's editor (<c>Field.Accessory</c>) for the window finder: a
/// <see cref="WindowFinder"/> whose pick goes to the form's view model, and the "Use" button that copies an identified
/// window's value into its field. Built per rendered row, so a rebuilt form gets fresh controls.
/// </summary>
public static class WindowFinderAccessory
{
    public const string UseLabel = "Use";

    /// <summary>A magnifier whose feedback reads <paramref name="describe"/> and whose pick calls <paramref name="picked"/>.</summary>
    public static Func<Control> Finder(Func<WindowIdentity, string> describe, Action<WindowIdentity> picked)
    {
        ArgumentNullException.ThrowIfNull(describe);
        ArgumentNullException.ThrowIfNull(picked);
        return () =>
        {
            var finder = new WindowFinder { Describe = describe };
            finder.Picked += (_, e) => picked(e.Window);
            return finder;
        };
    }

    /// <summary>A small toolbar button labelled <see cref="UseLabel"/> that calls <paramref name="use"/>.</summary>
    public static Func<Control> UseButton(Action use)
    {
        ArgumentNullException.ThrowIfNull(use);
        return () =>
        {
            var button = new Button { Content = UseLabel };
            button.Classes.Add("toolbar");
            button.Click += (_, _) => use();
            return button;
        };
    }
}
