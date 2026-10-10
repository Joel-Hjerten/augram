using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// The names already taken in one scope (the gestures, the groups, one group's categories or hold remaps, one command's
/// siblings: <see cref="CommandNames.SiblingNames"/>), compared like the rules compare them (<see cref="MappingRules.NameComparer"/>).
/// The first claim of a name keeps it; a later one gets the first free " (2)", " (3)"… suffix (F8 sync: name clashes are renamed).
/// </summary>
internal sealed class SyncNames
{
    private readonly HashSet<string> _taken;

    public SyncNames()
        : this([])
    {
    }

    /// <summary>A scope whose <paramref name="taken"/> names are already claimed.</summary>
    public SyncNames(IEnumerable<string> taken)
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
}
