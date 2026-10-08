using System.Reflection;

namespace Augram.App.Hosting;

/// <summary>
/// Which Augram this is: the product version (one place, <c>Directory.Build.props</c>), the commit it was built from
/// (the SDK appends the full hash to the informational version; <see cref="Commit"/> keeps the first
/// <see cref="ShortCommitLength"/> characters) and the build channel (MSBuild <c>AugramChannel</c>, embedded as
/// <c>[AssemblyMetadata("AugramChannel", …)]</c>): <see cref="AppChannel.Dev"/> for every local and CI build,
/// <see cref="AppChannel.Release"/> for the installed one the release script builds. A missing or unknown channel
/// reads as Dev, the safe side: a Dev build never touches start at login. The window title and the tray tooltip
/// start with <see cref="DisplayName"/>, so a development build always says "Augram (Dev)" (Joel, 2026-10-08).
/// </summary>
public sealed record AppInfo(string Version, string? Commit, AppChannel Channel)
{
    public const string ChannelMetadataKey = "AugramChannel";
    public const int ShortCommitLength = 7;
    public const string DevDisplayName = "Augram (Dev)";
    public const string ReleaseDisplayName = "Augram";

    /// <summary>This process's build, read once from the App assembly.</summary>
    public static AppInfo Current { get; } = FromAssembly(typeof(AppInfo).Assembly);

    /// <summary>"0.2.0+3f1c2ab", or the bare version when the build had no commit (no git at build time).</summary>
    public string InformationalVersion => Commit is null ? Version : Version + "+" + Commit;

    public bool IsDev => Channel == AppChannel.Dev;

    /// <summary>"Augram (Dev)" for a development build, "Augram" for the installed one: the window title and the start of the tray tooltip.</summary>
    public string DisplayName => DisplayNameOf(Channel);

    /// <summary>The channel as Options › About shows it.</summary>
    public string ChannelText => IsDev ? "Dev (development build)" : "Release (installed)";

    /// <summary>The second launch's dialog names a running build by this: "Augram 0.2.0 (installed)" or "Augram (Dev) 0.2.0".</summary>
    public string Describe() => IsDev ? $"{DevDisplayName} {Version}" : $"{ReleaseDisplayName} {Version} (installed)";

    public static string DisplayNameOf(AppChannel channel) => channel == AppChannel.Dev ? DevDisplayName : ReleaseDisplayName;

    /// <summary>Reads the informational version and the channel metadata of <paramref name="assembly"/>.</summary>
    public static AppInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var channel = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == ChannelMetadataKey)?.Value;
        return Parse(informational, channel);
    }

    /// <summary>
    /// From the attribute texts: <paramref name="informationalVersion"/> as the SDK writes it ("0.2.0+&lt;full hash&gt;", or
    /// "0.2.0+meta.&lt;hash&gt;" when the version already had build metadata) and the channel ("Release" in any case is
    /// Release; anything else is Dev).
    /// </summary>
    public static AppInfo Parse(string informationalVersion, string? channel)
    {
        ArgumentNullException.ThrowIfNull(informationalVersion);
        return new AppInfo(VersionPart(informationalVersion), CommitPart(informationalVersion), ParseChannel(channel));
    }

    public static AppChannel ParseChannel(string? channel) =>
        string.Equals(channel?.Trim(), nameof(AppChannel.Release), StringComparison.OrdinalIgnoreCase) ? AppChannel.Release : AppChannel.Dev;

    private static string VersionPart(string informational)
    {
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        var version = (plus < 0 ? informational : informational[..plus]).Trim();
        return version.Length > 0 ? version : "0.0.0";
    }

    private static string? CommitPart(string informational)
    {
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return null;
        }

        var metadata = informational[(plus + 1)..];
        var commit = metadata[(metadata.LastIndexOf('.') + 1)..].Trim();
        if (commit.Length == 0)
        {
            return null;
        }

        return commit.Length > ShortCommitLength ? commit[..ShortCommitLength] : commit;
    }
}
