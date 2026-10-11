using Augram.App.Hosting.Launch;

namespace Augram.App.Hosting.Modules;

/// <summary>What <see cref="ConfigModule.Register"/> lets the composition root (and tests) override; the defaults are the real thing.</summary>
public sealed record ConfigModuleOptions
{
    /// <summary>The config folder; null for <see cref="AppPaths.ConfigFolder"/>.</summary>
    public string? ConfigFolder { get; init; }

    /// <summary>Runs the debounced save on the settings writer thread; null for <see cref="UiThread.Post"/>.</summary>
    public Action<Action>? Marshal { get; init; }

    public static ConfigModuleOptions Default { get; } = new();
}
