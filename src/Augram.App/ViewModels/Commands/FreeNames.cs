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
