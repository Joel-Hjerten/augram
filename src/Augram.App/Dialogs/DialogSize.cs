using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Dialogs;

/// <summary>
/// The dialog windows' sizes, read from the <c>Dialog.*</c> tokens in <c>Themes/Tokens.axaml</c> (one set per window:
/// <c>Dialog.Form.Width</c>, <c>Dialog.UsedBy.MinHeight</c>…). A window names its set (<see cref="Form"/>, <see cref="Confirm"/>…)
/// and <see cref="Apply"/> sets the dimensions the set has; the gallery reads the same tokens to show a dialog at its window's width.
/// </summary>
public static class DialogSize
{
    public const string Form = "Dialog.Form";
    public const string Join = "Dialog.Join";
    public const string Confirm = "Dialog.Confirm";
    public const string UsedBy = "Dialog.UsedBy";
    public const string Conflicts = "Dialog.Conflicts";
    public const string Import = "Dialog.Import";
    public const string Picker = "Dialog.Picker";

    /// <summary>Sets each of Width, Height, MinWidth and MinHeight that the set <paramref name="size"/> has a token for; the others keep their defaults.</summary>
    public static void Apply(Window window, string size)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (TryGet(size + ".Width") is { } width)
        {
            window.Width = width;
        }

        if (TryGet(size + ".Height") is { } height)
        {
            window.Height = height;
        }

        if (TryGet(size + ".MinWidth") is { } minWidth)
        {
            window.MinWidth = minWidth;
        }

        if (TryGet(size + ".MinHeight") is { } minHeight)
        {
            window.MinHeight = minHeight;
        }
    }

    /// <summary>The set's width (the gallery, a request's own width); throws when the token is missing, as a typo would otherwise pass.</summary>
    public static double Width(string size) => Get(size + ".Width");

    /// <summary>The set's height; throws when the token is missing.</summary>
    public static double Height(string size) => Get(size + ".Height");

    private static double Get(string key) =>
        TryGet(key) ?? throw new InvalidOperationException($"No dialog size token '{key}' in the theme.");

    private static double? TryGet(string key) =>
        Application.Current is { } app && app.TryGetResource(key, null, out var value) && value is double size ? size : null;
}
