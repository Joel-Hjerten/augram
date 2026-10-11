using System.Globalization;

namespace Augram.Core.Text;

/// <summary>
/// The pieces the lines Augram writes for a person are made of: a count with its noun ("1 command", "3 commands", "no
/// commands"), a list joined the English way ("a, b and c") and a phrase made to start a sentence ("Scroll up"). Each has
/// one home here, so a screen, a dialog and the log word the same thing the same way (plan 0007 S3).
/// </summary>
public static class Sentence
{
    /// <summary>
    /// "1 command", "3 commands", "2 categories" (<paramref name="plural"/> when adding "s" is wrong); for 0 "0 commands",
    /// or "no commands" when <paramref name="zeroAsNo"/>. The digits are invariant.
    /// </summary>
    public static string Count(int count, string noun, string? plural = null, bool zeroAsNo = false)
    {
        ArgumentNullException.ThrowIfNull(noun);
        var nouns = plural ?? noun + "s";
        return count switch
        {
            0 when zeroAsNo => "no " + nouns,
            1 => "1 " + noun,
            _ => count.ToString(CultureInfo.InvariantCulture) + " " + nouns,
        };
    }

    /// <summary>"a", "a and b", "a, b and c"; empty for none.</summary>
    public static string List(IReadOnlyList<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }

    /// <summary><paramref name="text"/> with its first letter upper case ("scroll up" → "Scroll up"); empty stays empty.</summary>
    public static string Capitalised(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
