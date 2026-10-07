using System.Text.RegularExpressions;

namespace Augram.Sync.Git;

/// <summary>
/// Removes the <c>userinfo@</c> part from every URL in a text (<c>https://user:token@host/…</c> →
/// <c>https://host/…</c>), so no line this project returns or logs can carry a credential (F8 sync).
/// Applied to every error line, including ones git produced.
/// </summary>
internal static partial class CredentialScrubber
{
    public static string Scrub(string text) => UserInfo().Replace(text, "${scheme}");

    // scheme:// then everything up to the LAST '@' before the host's '/', whitespace or a quote, so a
    // password with a stray '@' in it goes too.
    [GeneratedRegex(@"(?<scheme>[A-Za-z][A-Za-z0-9+.\-]*://)[^/\s'""]*@", RegexOptions.CultureInvariant)]
    private static partial Regex UserInfo();
}
