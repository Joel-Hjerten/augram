using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Augram.Core.Mapping;

/// <summary>
/// The compiled form of every regex an <see cref="AppMatcher"/> has asked for, keyed by pattern, so
/// a match on the engine worker never constructs a <see cref="Regex"/> and the matcher records keep
/// value equality (no cached instance on the record). Patterns are few (one or two per app group);
/// the cache is emptied if it ever passes <see cref="Capacity"/>, which only typing into the
/// identification page can cause. An invalid pattern caches as null and matches nothing.
/// </summary>
internal static class MatcherRegexCache
{
    public const int Capacity = 256;

    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly ConcurrentDictionary<string, Regex?> Cache = new(StringComparer.Ordinal);

    /// <summary>False for an invalid pattern or a match that ran past <see cref="MatchTimeout"/>.</summary>
    public static bool IsMatch(string pattern, string input)
    {
        var regex = Get(pattern);
        if (regex is null)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>The parser's complaint about <paramref name="pattern"/>, or null when it is valid.</summary>
    public static string? Problem(string pattern)
    {
        if (Get(pattern) is not null)
        {
            return null;
        }

        try
        {
            _ = new Regex(pattern, Options, MatchTimeout);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    private static Regex? Get(string pattern)
    {
        if (Cache.TryGetValue(pattern, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= Capacity)
        {
            Cache.Clear();
        }

        return Cache.GetOrAdd(pattern, Compile);
    }

    private static Regex? Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, Options, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
