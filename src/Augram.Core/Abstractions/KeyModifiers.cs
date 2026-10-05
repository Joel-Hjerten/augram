namespace Augram.Core.Abstractions;

/// <summary>
/// Modifier keys held at the moment of an input event, as the input source reports them.
/// Left and right are folded together. Bit values match <c>Config.IgnoreKeys</c> so the
/// Options page's ignore-key set casts straight to a mask.
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
