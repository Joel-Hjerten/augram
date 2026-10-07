namespace Augram.Sync.Git;

/// <summary>
/// Who Augram's commits are by. The user's own git identity when one is configured; otherwise
/// "Augram (&lt;machine label&gt;)" &lt;augram@localhost&gt;, passed with <c>-c</c> for that one command
/// only. Augram never writes the user's git config.
/// </summary>
internal static class CommitIdentity
{
    public const string FallbackEmail = "augram@localhost";

    /// <summary>"Augram (label)", with the characters git refuses in a name ('&lt;', '&gt;', control characters) removed.</summary>
    public static string FallbackName(string? machineLabel)
    {
        var label = new string((machineLabel ?? string.Empty).Where(character => character is not ('<' or '>') && !char.IsControl(character)).ToArray()).Trim();
        return label.Length == 0 ? "Augram" : $"Augram ({label})";
    }

    /// <summary>The <c>-c</c> arguments that set the fallback identity, to go before git's subcommand.</summary>
    public static string[] FallbackArguments(string fallbackName) =>
        ["-c", "user.name=" + fallbackName, "-c", "user.email=" + FallbackEmail];
}
