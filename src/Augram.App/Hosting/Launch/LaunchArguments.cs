namespace Augram.App.Hosting.Launch;

/// <summary>
/// The switches a launch was started with, parsed once (<see cref="Parse"/>) by <c>Program.Main</c> before anything else reads
/// them and set as <see cref="Current"/>; the composition root registers it. Each switch keeps its own rule, beside the type
/// that owns its meaning: <c>--config-folder &lt;path&gt;</c> or <c>=path</c> (<see cref="AppPaths.ConfigFolderFrom"/>),
/// <c>--no-engine</c> or <c>AUGRAM_NO_ENGINE=1</c> (<see cref="EngineKillSwitch"/>) and <c>--hidden</c>
/// (<see cref="LaunchVisibility.HasHiddenArgument"/>), those three in any case; <c>--gallery</c> (Debug builds: open on the
/// dev gallery tab) only as written.
/// </summary>
public sealed record LaunchArguments(string? ConfigFolder, bool NoEngine, bool Hidden, bool Gallery)
{
    public const string GalleryArgument = "--gallery";

    private static readonly Lazy<LaunchArguments> FromProcess = new(() =>
        Parse(Environment.GetCommandLineArgs().Skip(1).ToArray(), Environment.GetEnvironmentVariable(EngineKillSwitch.EnvironmentVariable)));

    private static LaunchArguments? _set;

    /// <summary>
    /// This launch's switches: what <c>Program.Main</c> set. Until something sets it (the headless tests, <c>tools/UiShots</c>),
    /// the process's own arguments and environment, parsed the same way.
    /// </summary>
    public static LaunchArguments Current
    {
        get => _set ?? FromProcess.Value;
        set => _set = value;
    }

    /// <param name="args">The launch's arguments, without the executable.</param>
    /// <param name="noEngineVariable">The value of <see cref="EngineKillSwitch.EnvironmentVariable"/>; null when it is unset.</param>
    public static LaunchArguments Parse(IReadOnlyList<string> args, string? noEngineVariable)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new LaunchArguments(
            AppPaths.ConfigFolderFrom(args),
            EngineKillSwitch.IsSet(args, noEngineVariable),
            LaunchVisibility.HasHiddenArgument(args),
            args.Contains(GalleryArgument, StringComparer.Ordinal));
    }
}
