namespace Augram.Core.Abstractions;

/// <summary>
/// Modifier keys held at the moment of an input event, as the input source reports them.
/// Left and right are folded together. Also the Options page's ignore-key set
/// (<c>Config.GeneralSettings.IgnoreKey</c>, written "Win" for <see cref="Meta"/> as the file always has).
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,

    /// <summary>Win on Windows, Command on macOS.</summary>
    Meta = 8,
}
