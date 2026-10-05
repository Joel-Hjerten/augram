using Augram.Core.Config;

namespace Augram.App.Hosting;

/// <summary>
/// Where the app keeps its files: the config folder and <c>logs/</c> beside it (N4). The folder is the
/// per-platform default unless the process was started with <c>--config-folder &lt;path&gt;</c>
/// (checklist A17, minimal form: an argument, no UI yet; the Options page shows the folder read-only).
/// </summary>
public static class AppPaths
{
    public const string ConfigFolderArgument = "--config-folder";

    private static readonly Lazy<string?> FromCommandLine = new(() => ConfigFolderFrom(Environment.GetCommandLineArgs().Skip(1).ToArray()));

    public static string ConfigFolder => FromCommandLine.Value ?? DefaultConfigFolder.Resolve();

    public static string LogsFolder => Path.Combine(ConfigFolder, "logs");

    /// <summary>
    /// The folder named by <c>--config-folder &lt;path&gt;</c> or <c>--config-folder=&lt;path&gt;</c>, made
    /// absolute against the working directory; null when the argument is absent or has no value.
    /// </summary>
    public static string? ConfigFolderFrom(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? value = null;
            if (string.Equals(arg, ConfigFolderArgument, StringComparison.OrdinalIgnoreCase))
            {
                value = i + 1 < args.Count ? args[i + 1] : null;
            }
            else if (arg.StartsWith(ConfigFolderArgument + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(ConfigFolderArgument.Length + 1)..];
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return Path.GetFullPath(value.Trim().Trim('"'));
            }
        }

        return null;
    }
}
