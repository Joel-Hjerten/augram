using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The names the Commands tab makes up for a fresh item or a pasted copy, free among <c>taken</c> (compared as the rules compare names).</summary>
internal static class FreeNames
{
    /// <summary>"<paramref name="stem"/> N" with the smallest N not taken ("New command 1", "New category 2").</summary>
    public static string Next(string stem, IEnumerable<string> taken)
    {
        var names = taken.ToHashSet(MappingRules.NameComparer);
        for (var n = 1; ; n++)
        {
            var candidate = $"{stem} {n}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>The name <see cref="Next"/> (or <paramref name="stem"/> alone) makes: "Hold remap", "Hold remap 2"; compared as the rules compare names.</summary>
    public static bool IsNumbered(string stem, string name)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(name);
        if (MappingRules.NameComparer.Equals(name, stem))
        {
            return true;
        }

        return name.Length > stem.Length + 1
            && MappingRules.NameComparer.Equals(name[..stem.Length], stem)
            && name[stem.Length] == ' '
            && name[(stem.Length + 1)..].All(char.IsAsciiDigit);
    }

    /// <summary>The name itself when it is free, else "name 2", "name 3", … (an app added from a picked window: "Spine 2").</summary>
    public static string Free(string name, IEnumerable<string> taken)
    {
        var names = taken.ToHashSet(MappingRules.NameComparer);
        if (!names.Contains(name))
        {
            return name;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{name} {n}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>The name itself when it is free, else "name copy", "name copy 2", …</summary>
    public static string CopyOf(string name, IEnumerable<string> taken)
    {
        var names = taken.ToHashSet(MappingRules.NameComparer);
        if (!names.Contains(name))
        {
            return name;
        }

        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? $"{name} copy" : $"{name} copy {n}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
