namespace Augram.App.Components.SearchBox;

/// <summary>
/// The one rule every list's search follows (plan 0009 decision 2): the box's text split into words at spaces, and a name
/// matches when every word is a case-insensitive substring of it ("down left" finds "Down then left"). No fuzzy matching.
/// A row with two texts (an excluded app's name and summary) matches when every word is in one of them. An empty query
/// (nothing, or spaces) matches everything. <see cref="StartsWith"/> is the gesture picker's ordering: the gestures whose
/// name starts with the typed text come first. Nothing else in the App compares text for a search
/// (<c>OneHomeComponentTests.SearchesCompareTextOnlyInSearchQuery</c>).
/// </summary>
public sealed class SearchQuery
{
    public static readonly SearchQuery Empty = new(string.Empty, []);

    private readonly string[] _words;

    private SearchQuery(string text, string[] words)
    {
        Text = text;
        _words = words;
    }

    /// <summary>The typed text, trimmed; what the empty state quotes.</summary>
    public string Text { get; }

    public bool IsEmpty => _words.Length == 0;

    public static SearchQuery Parse(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? Empty : new SearchQuery(trimmed, words);
    }

    /// <summary>Every word is somewhere in <paramref name="name"/>, case-insensitive; an empty query matches any name.</summary>
    public bool Matches(string? name) => IsEmpty || (name is not null && _words.All(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Every word is somewhere in <paramref name="name"/> or in <paramref name="summary"/> (an excluded app's name or its summary).</summary>
    public bool Matches(string? name, string? summary)
        => IsEmpty || _words.All(word => name?.Contains(word, StringComparison.OrdinalIgnoreCase) == true || summary?.Contains(word, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>The name starts with the whole typed text, case-insensitive; never for an empty query.</summary>
    public bool StartsWith(string? name) => !IsEmpty && name is not null && name.StartsWith(Text, StringComparison.OrdinalIgnoreCase);

    /// <summary>The empty state's sentence (decision 7): "No commands match 'xyz'".</summary>
    public string NoneMatch(string plural) => $"No {plural} match '{Text}'";
}
