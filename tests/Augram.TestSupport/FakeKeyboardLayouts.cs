using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>
/// Fake <see cref="IKeyboardLayouts"/> (plan 0011): the layouts handed to the constructor are <see cref="Installed"/>, the first
/// of them is <see cref="Active"/> until a test sets it (null: the platform cannot say), and <see cref="Characters"/> is what
/// <see cref="CharacterOf"/> answers (nothing for a key not in it). <see cref="ActiveReads"/> counts the reads of
/// <see cref="Active"/>, so a test can prove a step asked once, or never. Not thread-safe. <see cref="Swedish"/> and
/// <see cref="Abc"/> are the two layouts Joel switches between on the Mac.
/// </summary>
public sealed class FakeKeyboardLayouts : IKeyboardLayouts
{
    public static readonly KeyboardLayout Swedish = new("com.apple.keylayout.Swedish-Pro", "Swedish");

    public static readonly KeyboardLayout Abc = new("com.apple.keylayout.ABC", "ABC");

    private KeyboardLayout? _active;

    public FakeKeyboardLayouts(params KeyboardLayout[] installed)
    {
        Installed = installed;
        _active = installed.Length > 0 ? installed[0] : null;
    }

    /// <summary>How many times <see cref="Active"/> was read.</summary>
    public int ActiveReads { get; private set; }

    public KeyboardLayout? Active
    {
        get
        {
            ActiveReads++;
            return _active;
        }
        set => _active = value;
    }

    public IReadOnlyList<KeyboardLayout> Installed { get; set; }

    /// <summary>What each key prints under the active layout; a key not listed prints nothing.</summary>
    public Dictionary<KeyCode, string> Characters { get; } = [];

    public string? CharacterOf(KeyCode key) => Characters.TryGetValue(key, out var character) ? character : null;
}
