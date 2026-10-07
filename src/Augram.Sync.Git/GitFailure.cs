namespace Augram.Sync.Git;

/// <summary>
/// Turns a failed git call into one line for the log and Options › Sync. Recognised causes get a
/// fixed sentence; anything else is git's last stderr line. Every line is scrubbed of credentials.
/// Matching relies on English messages, which <see cref="GitRunner"/> guarantees with <c>LC_ALL=C</c>.
/// </summary>
internal static class GitFailure
{
    public const string NotInstalled = "git is not installed or not on PATH";
    public const string Offline = "offline or the host cannot be reached";
    public const string NotFound = "the repository was not found or this account cannot see it";

    private static readonly string[] SignInMarkers =
    [
        "Authentication failed", "could not read Username", "could not read Password", "terminal prompts disabled",
        "Invalid username or password", "Invalid username or token", "HTTP Basic: Access denied",
        "returned error: 401", "returned error: 403", "Permission denied (publickey",
    ];

    private static readonly string[] NotFoundMarkers = ["not found", "does not exist", "does not appear to be a git repository"];

    private static readonly string[] OfflineMarkers =
    [
        "Could not resolve host", "Could not resolve hostname", "Temporary failure in name resolution",
        "Failed to connect", "Couldn't connect to server", "Connection timed out", "Operation timed out",
        "Connection refused", "Connection reset", "Network is unreachable", "No route to host",
    ];

    private static readonly string[] MessagePrefixes = ["fatal: ", "error: ", "remote: "];

    public static string SignInNeeded(string folder) =>
        $"sign-in needed: GitHub refused the credentials; push once from a terminal in {folder} or sign in with Git Credential Manager";

    /// <summary>The line for a call that did not succeed; <paramref name="operation"/> is git's subcommand ("push").</summary>
    public static string Describe(GitResult result, string operation, string folder) =>
        CredentialScrubber.Scrub(Classify(result, operation, folder));

    private static string Classify(GitResult result, string operation, string folder)
    {
        if (result.Status == GitRunStatus.NotInstalled)
        {
            return NotInstalled;
        }

        if (result.Status == GitRunStatus.TimedOut)
        {
            return $"git {operation} did not finish in time and was stopped; try again later";
        }

        var text = result.Error + "\n" + result.Output;
        if (ContainsAny(text, SignInMarkers))
        {
            return SignInNeeded(folder);
        }

        if (Lines(text).Any(line => line.Contains("repository", StringComparison.OrdinalIgnoreCase) && ContainsAny(line, NotFoundMarkers)))
        {
            return NotFound;
        }

        if (ContainsAny(text, OfflineMarkers))
        {
            return Offline;
        }

        var last = LastMessage(result.Error) ?? LastMessage(result.Output);
        return last is null
            ? $"git {operation} failed with exit code {result.ExitCode}"
            : $"git {operation} failed: {last}";
    }

    private static bool ContainsAny(string text, string[] markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // The last line that says something: hints follow the real error on a rejected push.
    private static string? LastMessage(string text)
    {
        var line = Lines(text).LastOrDefault(candidate => !candidate.StartsWith("hint:", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        foreach (var prefix in MessagePrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..];
            }
        }

        return line;
    }
}
