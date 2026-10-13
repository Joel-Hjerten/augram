namespace Augram.Core.Abstractions;

/// <summary>
/// One keyboard layout as the OS lists it (plan 0011): <see cref="Id"/> is the platform's own id, stored as text in a
/// Hotkey step's <c>layouts</c> (macOS: the input source id, <c>com.apple.keylayout.Swedish-Pro</c>; Windows: the keyboard
/// layout id, <c>0000041D</c>), and <see cref="Name"/> is what the OS shows for it ("Swedish", "ABC"). The ids of the two
/// platforms never meet: a layout shortcut belongs to the side it was authored on.
/// </summary>
public sealed record KeyboardLayout(string Id, string Name);
