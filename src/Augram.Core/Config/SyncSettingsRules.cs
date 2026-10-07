using System.Text.RegularExpressions;

namespace Augram.Core.Config;

/// <summary>
/// The rules for <see cref="SyncSettings"/> (ADR-0002 §5a: one home; <see cref="SettingsRules"/> calls
/// <see cref="EnsureValid"/>, the Options page shows the message). The repository is an <c>https://</c> URL,
/// an scp-style <c>git@host:path</c>, or a full local path (or <c>file://</c> URL), and never carries a user
/// name or token: authentication is git's credential helper (F8 sync), so a URL like
/// <c>https://user:token@github.com/…</c> is refused with a message that says why.
/// </summary>
public static partial class SyncSettingsRules
{
    public const string LocalHost = "local folder";

    public const string UserInfoProblem =
        "Remove the user name or token from the URL (the 'user:token@' part). Augram never stores credentials: git's credential helper signs you in on the first sync.";

    /// <summary>Trims the URL (blank is null: sync off) and the machine name, and keeps <paramref name="currentMachineId"/> when <paramref name="sync"/> has none.</summary>
    public static SyncSettings Normalised(SyncSettings sync, Guid currentMachineId)
    {
        ArgumentNullException.ThrowIfNull(sync);
        return sync with
        {
            RepositoryUrl = string.IsNullOrWhiteSpace(sync.RepositoryUrl) ? null : sync.RepositoryUrl.Trim(),
            MachineName = sync.MachineName?.Trim() ?? string.Empty,
            MachineId = sync.MachineId == Guid.Empty ? currentMachineId : sync.MachineId,
        };
    }

    /// <exception cref="SettingsValidationException">The machine name is blank or the URL is not acceptable.</exception>
    public static void EnsureValid(SyncSettings sync)
    {
        ArgumentNullException.ThrowIfNull(sync);
        if (string.IsNullOrWhiteSpace(sync.MachineName))
        {
            throw new SettingsValidationException("This machine needs a name for sync.");
        }

        if (sync.RepositoryUrl is { } url && UrlProblem(url) is { } problem)
        {
            throw new SettingsValidationException(problem);
        }
    }

    /// <summary>Null when <paramref name="url"/> is acceptable; otherwise one sentence for the Options page.</summary>
    public static string? UrlProblem(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var text = url.Trim();
        if (text.Length == 0)
        {
            return "Enter the repository URL, or clear it to turn sync off.";
        }

        if (ScpStyle().Match(text) is { Success: true })
        {
            return null;
        }

        if (text.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            return "A git@ URL looks like git@github.com:you/augram-settings.git.";
        }

        if (Path.IsPathFullyQualified(text))
        {
            return null;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return "Use an https:// URL, git@host:path, or the full path of a folder.";
        }

        if (uri.Scheme == "ssh")
        {
            return "For SSH use the git@host:path form, e.g. git@github.com:you/augram-settings.git.";
        }

        if (uri.UserInfo.Length > 0)
        {
            return UserInfoProblem;
        }

        return uri.Scheme switch
        {
            _ when uri.IsFile => null,
            "https" when uri.Host.Length > 0 => null,
            "http" => "Use https:// rather than http://, so the sign-in never travels unencrypted.",
            _ => "Use an https:// URL, git@host:path, or the full path of a folder.",
        };
    }

    /// <summary>What the log may say about the repository: the host ("github.com") or <see cref="LocalHost"/>; never a path, a user name or a token.</summary>
    public static string Host(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "none";
        }

        var text = url.Trim();
        if (ScpStyle().Match(text) is { Success: true } scp)
        {
            return scp.Groups["host"].Value;
        }

        if (Path.IsPathFullyQualified(text))
        {
            return LocalHost;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return uri.IsFile ? LocalHost : uri.Host;
        }

        return "unknown";
    }

    [GeneratedRegex(@"^git@(?<host>[^\s:/@]+):(?<path>[^\s@]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpStyle();
}
