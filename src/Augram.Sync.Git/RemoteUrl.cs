namespace Augram.Sync.Git;

/// <summary>
/// Remote URL rules for <see cref="GitSyncRepository.Prepare"/>: when two spellings name the same
/// repository, and when a URL carries a credential Augram must not store.
/// </summary>
internal static class RemoteUrl
{
    private const string SchemeSeparator = "://";

    /// <summary>
    /// Same repository: scheme and host compared case-insensitively, user names ignored, a trailing
    /// <c>.git</c> or <c>/</c> ignored. Local paths compare with either slash, case-insensitively on Windows.
    /// </summary>
    public static bool SameRepository(string first, string second) =>
        string.Equals(Normalize(first), Normalize(second), StringComparison.Ordinal);

    /// <summary>
    /// True for an http(s) URL with any user part (a token can stand in the user name) and for any URL
    /// with a password. Cloning one would store the credential in the clone's config in plain text.
    /// <c>ssh://git@host/…</c> and <c>git@host:…</c> carry no secret and pass.
    /// </summary>
    public static bool CarriesCredentials(string url)
    {
        if (!TrySplit(url.Trim(), out var scheme, out var authority, out _))
        {
            return false;
        }

        var at = authority.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var isHttp = scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        return isHttp || authority[..at].Contains(':', StringComparison.Ordinal);
    }

    internal static string Normalize(string url)
    {
        var text = url.Trim();
        if (TrySplit(text, out var scheme, out var authority, out var path))
        {
            text = scheme.ToLowerInvariant() + SchemeSeparator + HostOf(authority).ToLowerInvariant() + path;
        }
        else if (IsScpLike(text, out var colon))
        {
            text = HostOf(text[..colon]).ToLowerInvariant() + text[colon..];
        }
        else
        {
            text = text.Replace('\\', '/');
            if (OperatingSystem.IsWindows())
            {
                text = text.ToLowerInvariant();
            }
        }

        text = text.TrimEnd('/');
        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }

        return text.TrimEnd('/');
    }

    private static bool TrySplit(string url, out string scheme, out string authority, out string path)
    {
        var separator = url.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (separator <= 0)
        {
            scheme = authority = path = string.Empty;
            return false;
        }

        scheme = url[..separator];
        var rest = url[(separator + SchemeSeparator.Length)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        authority = slash < 0 ? rest : rest[..slash];
        path = slash < 0 ? string.Empty : rest[slash..];
        return true;
    }

    // git's "user@host:path" form: a colon before any slash, and more than a drive letter before it.
    private static bool IsScpLike(string text, out int colon)
    {
        colon = text.IndexOf(':', StringComparison.Ordinal);
        var slash = text.IndexOfAny(['/', '\\']);
        return colon > 1 && (slash < 0 || colon < slash);
    }

    private static string HostOf(string authority) => authority[(authority.LastIndexOf('@') + 1)..];
}
