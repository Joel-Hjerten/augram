namespace Augram.Core.Abstractions;

/// <summary>Knows no layout: the default before the App sets the port, on a platform without an adapter, and in tests. Key names stay the US ones.</summary>
public sealed class NullKeyboardLayouts : IKeyboardLayouts
{
    public static NullKeyboardLayouts Instance { get; } = new();

    private NullKeyboardLayouts()
    {
    }

    public KeyboardLayout? Active => null;

    public IReadOnlyList<KeyboardLayout> Installed => [];

    public string? CharacterOf(KeyCode key, string? layoutId = null) => null;
}
