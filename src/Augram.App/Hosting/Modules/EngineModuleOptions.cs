using Augram.Core.Abstractions;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// What <see cref="EngineModule.Register"/> lets the composition root (and tests) override. Every member is optional; the
/// defaults are the real thing: SharpHook, the platform's simulator wrappers, and the Avalonia UI thread as the writer thread.
/// </summary>
public sealed record EngineModuleOptions
{
    /// <summary>The input source factory; null for <c>SharpHookInputSource</c>. Tests pass a fake so no hook is installed.</summary>
    public Func<IServiceProvider, IInputSource>? InputSource { get; init; }

    /// <summary>False leaves SharpHook's simulator unwrapped on macOS; the root passes what it passes <see cref="PlatformModule.Register"/>.</summary>
    public bool PlatformAdapters { get; init; } = true;

    /// <summary>Runs an action on the settings writer thread; null for <see cref="UiThread.Post"/>.</summary>
    public Action<Action>? Marshal { get; init; }

    public static EngineModuleOptions Default { get; } = new();
}
