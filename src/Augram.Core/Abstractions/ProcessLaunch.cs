namespace Augram.Core.Abstractions;

/// <summary>
/// One request to <see cref="IProcessLauncher.Launch"/>, as the user wrote it: <paramref name="File"/> is a program
/// (path or bare name such as <c>explorer</c>), a document, a folder or a URI (<c>ms-settings:display</c>,
/// <c>https://…</c>); <paramref name="Arguments"/> is one command-line string; an empty
/// <paramref name="WorkingDirectory"/> means the adapter's default (the user's home folder). Environment variables
/// (<c>%WINDIR%</c>, <c>~</c> on macOS) are expanded by the adapter, in the file and the working directory only.
/// <paramref name="Elevated"/> asks for administrator rights (UAC on Windows; not supported on macOS);
/// <paramref name="Hidden"/> starts the window hidden (Windows) or in the background (macOS).
/// </summary>
public sealed record ProcessLaunch(string File, string Arguments = "", string WorkingDirectory = "", bool Elevated = false, bool Hidden = false)
{
    /// <summary>The URI scheme of <see cref="File"/> ("https", "ms-settings"), or null when it is a program, path or bare name.</summary>
    public string? UriScheme => SchemeOf(File);

    /// <summary>
    /// The scheme of <paramref name="target"/> when it reads as a URI: two or more characters before the first colon, a
    /// letter first, then letters, digits, <c>+</c>, <c>-</c> or <c>.</c> (RFC 3986). A drive letter (<c>C:\…</c>) has one
    /// character and is a path, not a scheme. Null for anything else.
    /// </summary>
    public static string? SchemeOf(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var text = target.AsSpan().Trim();
        var colon = text.IndexOf(':');
        if (colon < 2 || !char.IsAsciiLetter(text[0]))
        {
            return null;
        }

        foreach (var c in text[..colon])
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
            {
                return null;
            }
        }

        return text[..colon].ToString();
    }
}
