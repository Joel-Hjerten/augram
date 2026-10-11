using System.Globalization;

namespace Augram.Core.Mapping;

/// <summary>
/// The names made up for a fresh item, free among <c>taken</c>: "New command 1", "New category 2", "New gesture 3", "Hold
/// remap" then "Hold remap 1". The caller passes the comparer its rules compare names with (<see cref="MappingRules.NameComparer"/>,
/// <see cref="Gestures.GestureRules.NameComparer"/>). A paste, a copy or a picked app whose name is taken is renamed by the
/// one rule for a clash instead (<see cref="NameScope"/>: "Orbit (2)").
/// </summary>
public static class FreeNames
{
    /// <summary>
    /// "<paramref name="stem"/> N" with the smallest N (from 1) not in <paramref name="taken"/>; when <paramref name="stemFirst"/>,
    /// <paramref name="stem"/> alone while it is free.
    /// </summary>
    public static string Next(string stem, IEnumerable<string> taken, StringComparer comparer, bool stemFirst = false)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(comparer);
        var names = taken.ToHashSet(comparer);
        if (stemFirst && !names.Contains(stem))
        {
            return stem;
        }

        for (var n = 1; ; n++)
        {
            var candidate = stem + " " + n.ToString(CultureInfo.InvariantCulture);
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>True for a name <see cref="Next"/> makes from <paramref name="stem"/> (or <paramref name="stem"/> alone): "Hold remap", "Hold remap 2".</summary>
    public static bool IsNumbered(string stem, string name, StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(comparer);
        if (comparer.Equals(name, stem))
        {
            return true;
        }

        return name.Length > stem.Length + 1
            && comparer.Equals(name[..stem.Length], stem)
            && name[stem.Length] == ' '
            && name[(stem.Length + 1)..].All(char.IsAsciiDigit);
    }
}
