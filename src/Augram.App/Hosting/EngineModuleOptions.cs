using Augram.Core.Abstractions;

namespace Augram.App.Hosting;

/// <summary>
/// What <see cref="EngineModule.Register"/> lets the composition root (and tests) override. Every
/// member is optional; the defaults are the real thing: the per-platform config folder, SharpHook, the
/// Windows adapters, and the Avalonia UI thread as the writer thread.
/// </summary>
public sealed record EngineModuleOptions
{
    /// <summary>The config folder; null for <see cref="AppPaths.ConfigFolder"/>.</summary>
    public string? ConfigFolder { get; init; }

    /// <summary>The input source factory; null for <c>SharpHookInputSource</c>. Tests pass a fake so no hook is installed.</summary>
    public Func<IServiceProvider, IInputSource>? InputSource { get; init; }

    /// <summary>False registers the null adapters (no cursor probe, no system events, no Win32 styles, in-memory start-at-login) instead of the Platform ones.</summary>
    public bool PlatformAdapters { get; init; } = true;

    /// <summary>Runs an action on the settings writer thread; null for <c>Dispatcher.UIThread.Post</c>.</summary>
    public Action<Action>? Marshal { get; init; }

    public static EngineModuleOptions Default { get; } = new();
}
