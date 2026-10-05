namespace Augram.Core.Diagnostics;

/// <summary>
/// What the recognition log panel shows per stroke (A15): the stroke's size and duration, the best
/// candidates with scores (at most <see cref="MaxTopMatches"/>, best first), which app group was
/// matched, and either the command that fired or why nothing did. At most one of
/// <paramref name="FiredCommand"/> and <paramref name="NothingFiredReason"/> is expected to be set.
/// </summary>
public sealed record RecognitionLogEntry(
    DateTimeOffset Timestamp,
    int PointCount,
    int DurationMs,
    IReadOnlyList<RecognitionCandidate> TopMatches,
    string? MatchedGroup = null,
    string? FiredCommand = null,
    string? NothingFiredReason = null)
{
    public const int MaxTopMatches = 3;

    /// <summary>Best first; trimmed to <see cref="MaxTopMatches"/> on construction.</summary>
    public IReadOnlyList<RecognitionCandidate> TopMatches { get; init; } = Trim(TopMatches);

    private static IReadOnlyList<RecognitionCandidate> Trim(IReadOnlyList<RecognitionCandidate> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        return matches.Count <= MaxTopMatches ? matches : matches.Take(MaxTopMatches).ToArray();
    }
}
