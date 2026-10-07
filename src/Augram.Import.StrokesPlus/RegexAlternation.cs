using System.Text;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Recognises the one regex idiom SP.net users write for "any of these names": a plain alternation of
/// literals, <c>chrome\.exe|msedge\.exe</c>, optionally anchored or wrapped in one group, and splits
/// it into the names (F5: Augram's matcher takes a list, so no regex survives the import). Anything
/// with a real metacharacter left (<c>PotPlayerMini.*\.exe</c>, <c>Spine(?:-1)?\.exe</c>) is not plain.
/// </summary>
internal static class RegexAlternation
{
    private const string Metacharacters = ".*+?[]{}()^$|\\";

    public static bool TryReadLiterals(string pattern, out IReadOnlyList<string> literals)
    {
        literals = [];
        var names = new List<string>();
        foreach (var part in Unwrapped(pattern.Trim()).Split('|'))
        {
            if (!TryUnescape(part, out var literal) || literal.Length == 0)
            {
                return false;
            }

            names.Add(literal);
        }

        literals = names;
        return names.Count > 0;
    }

    private static string Unwrapped(string pattern)
    {
        if (pattern.StartsWith('^'))
        {
            pattern = pattern[1..];
        }

        if (pattern.EndsWith('$') && !pattern.EndsWith("\\$", StringComparison.Ordinal))
        {
            pattern = pattern[..^1];
        }

        if (pattern.StartsWith("(?:", StringComparison.Ordinal) && pattern.EndsWith(')'))
        {
            return pattern[3..^1];
        }

        return pattern.StartsWith('(') && pattern.EndsWith(')') ? pattern[1..^1] : pattern;
    }

    /// <summary>The part with its escapes removed; a bare metacharacter or a letter escape (<c>\d</c>) means it is not a literal.</summary>
    private static bool TryUnescape(string part, out string literal)
    {
        literal = string.Empty;
        var text = new StringBuilder(part.Length);
        for (var i = 0; i < part.Length; i++)
        {
            var c = part[i];
            if (c == '\\')
            {
                if (i + 1 >= part.Length || char.IsLetterOrDigit(part[i + 1]))
                {
                    return false;
                }

                text.Append(part[++i]);
                continue;
            }

            if (Metacharacters.Contains(c))
            {
                return false;
            }

            text.Append(c);
        }

        literal = text.ToString().Trim();
        return true;
    }
}
