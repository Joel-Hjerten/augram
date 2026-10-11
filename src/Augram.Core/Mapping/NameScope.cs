namespace Augram.Core.Mapping;

/// <summary>
/// The names already taken in one scope (the gestures, the groups, one group's categories or hold remaps, one command's
/// siblings: <see cref="CommandNames.SiblingNames"/>), compared like the rules compare them (<see cref="MappingRules.NameComparer"/>).
/// The first claim of a name keeps it; a later one gets the first free " (2)", " (3)"… suffix. The one rule for a name that
/// clashes (Joel, 2026-10-11): a paste or a copy, an app added from a picked window, the sync's rename, an Augram file's
/// import and the StrokesPlus.net import all claim through this.
/// </summary>
public sealed class NameScope
{
    private readonly HashSet<string> _taken;

    public NameScope()
        : this([])
    {
    }

    /// <summary>A scope whose <paramref name="taken"/> names are already claimed.</summary>
    public NameScope(IEnumerable<string> taken)
    {
        _taken = new HashSet<string>(taken, MappingRules.NameComparer);
    }

    /// <summary>The name to use: <paramref name="name"/> when free, else the first free suffixed form. Either way it is taken afterwards.</summary>
    public string Claim(string name)
    {
        if (_taken.Add(name))
        {
            return name;
        }

        for (int n = 2; ; n++)
        {
            var candidate = $"{name} ({n})";
            if (_taken.Add(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary><paramref name="name"/> when no name in <paramref name="taken"/> is the same, else its first free " (N)" form: one claim in a new scope.</summary>
    public static string Free(string name, IEnumerable<string> taken) => new NameScope(taken).Claim(name);
}
