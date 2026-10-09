namespace Augram.Import.StrokesPlus;

/// <summary>
/// A script's code as a list of tokens (<see cref="ScriptReader.TryToken"/>), with comments, blanks, line breaks and
/// semicolons left out, so two scripts that differ only in those compare equal: how <see cref="ScriptMapping"/> knows a
/// script is one of SP.net's samples (<see cref="SampleScripts"/>) however it was laid out or commented. Pure.
/// </summary>
internal static class ScriptTokens
{
    /// <summary>The tokens of <paramref name="script"/>; empty when it holds only comments and blanks; null when a comment or a string is not closed.</summary>
    public static IReadOnlyList<string>? Read(string script)
    {
        var reader = new ScriptReader(script);
        var tokens = new List<string>();
        while (reader.TryToken() is { } token)
        {
            if (token != ";")
            {
                tokens.Add(token);
            }
        }

        return reader.Error is null ? tokens : null;
    }

    /// <summary>True when both lists hold the same tokens in the same order (JavaScript is case-sensitive, so is this).</summary>
    public static bool Same(IReadOnlyList<string> tokens, IReadOnlyList<string> other)
        => tokens.SequenceEqual(other, StringComparer.Ordinal);
}
