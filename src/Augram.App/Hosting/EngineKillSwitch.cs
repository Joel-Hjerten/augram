namespace Augram.App.Hosting;

/// <summary>
/// Keeps the hook and the overlay off for a run: the <c>--no-engine</c> argument or the environment
/// variable <c>AUGRAM_NO_ENGINE=1</c>. For agents and tests that need the UI without touching the
/// machine's input (a wrong overlay style or a wrong suppression decision blocks the user's mouse).
/// </summary>
public static class EngineKillSwitch
{
    public const string Argument = "--no-engine";
    public const string EnvironmentVariable = "AUGRAM_NO_ENGINE";

    public static bool IsSet() =>
        IsSet(Environment.GetCommandLineArgs().Skip(1).ToArray(), Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static bool IsSet(IReadOnlyList<string> args, string? environmentValue)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase))
            || string.Equals(environmentValue?.Trim(), "1", StringComparison.Ordinal);
    }
}
