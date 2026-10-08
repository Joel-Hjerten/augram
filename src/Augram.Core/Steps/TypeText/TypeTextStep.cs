namespace Augram.Core.Steps.TypeText;

/// <summary>
/// Types <see cref="Text"/> into whatever has focus (F5 "type a string"; Joel's SP.net config has 18 of these, mostly
/// game console commands), by <see cref="Method"/>. A line break (CR LF, CR or LF) is pressed as the Enter key, so a
/// multi-line text types its lines with Enter between them whatever the method. An empty text is the "no text set
/// yet" a new step starts with; running it skips. The text is kept exactly as stored: nothing trims or normalises it.
/// </summary>
public sealed record TypeTextStep(string Text, TypeTextMethod Method = TypeTextMethod.Unicode) : IStep
{
    public const string UnsetSummary = "Type text (no text set)";

    /// <summary>The longest text the summary shows, ellipsis included.</summary>
    public const int SummaryTextLength = 40;

    /// <summary>What a line break reads as in the summary: one line on the command row, the breaks still visible.</summary>
    public const string LineBreakMark = "⏎";

    private static readonly string[] LineBreaks = ["\r\n", "\r", "\n"];

    /// <summary>No text, Unicode: what the picker adds.</summary>
    public static TypeTextStep Empty { get; } = new(string.Empty);

    public IStepType Type => TypeTextStepType.Instance;

    public bool HasText => !string.IsNullOrEmpty(Text);

    /// <summary>
    /// <c>Type "fov 67.5⏎"</c>, <c>Type "the first forty characters of a long tex…"</c>, <c>Type "`" (by keys)</c>;
    /// <see cref="UnsetSummary"/> while the text is empty.
    /// </summary>
    public string Summary => HasText
        ? $"Type \"{Shorten(string.Join(LineBreakMark, Lines()))}\"" + (Method == TypeTextMethod.Keys ? " (by keys)" : string.Empty)
        : UnsetSummary;

    /// <summary>The text split at its line breaks (CR LF counts once); one empty line for an empty text.</summary>
    public IReadOnlyList<string> Lines() => (Text ?? string.Empty).Split(LineBreaks, StringSplitOptions.None);

    private static string Shorten(string text)
    {
        if (text.Length <= SummaryTextLength)
        {
            return text;
        }

        var cut = SummaryTextLength - 1;
        if (char.IsHighSurrogate(text[cut - 1]))
        {
            cut--;
        }

        return text[..cut] + "…";
    }
}
