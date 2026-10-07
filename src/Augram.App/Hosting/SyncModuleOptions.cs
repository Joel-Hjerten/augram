using Augram.Core.Abstractions;
using Augram.Core.Sync;

namespace Augram.App.Hosting;

/// <summary>
/// What <see cref="SyncModule.Register"/> lets the composition root (and tests) override. Every member is optional;
/// the defaults are the real thing: the per-platform config folder, the installed git, the Avalonia UI thread as the
/// store thread and the marshal target, and real timers.
/// </summary>
public sealed record SyncModuleOptions
{
    /// <summary>The config folder; null for <see cref="AppPaths.ConfigFolder"/>. Sync keeps <c>sync/</c> there.</summary>
    public string? ConfigFolder { get; init; }

    /// <summary>The repository factory, given the clone folder; null for <c>GitSyncRepository</c>. Tests pass a fake: no git, no network.</summary>
    public Func<string, ISyncRepository>? Repository { get; init; }

    /// <summary>Runs a sync's store changes on the store thread; null for the Avalonia UI thread. Tests pass <c>(f, _) =&gt; f()</c>.</summary>
    public Func<Func<SyncApplied>, CancellationToken, SyncApplied>? StoreThread { get; init; }

    /// <summary>One-shot timers for the change delay and the poll; null for real timers.</summary>
    public Func<TimeSpan, Action, IDisposable>? Schedule { get; init; }

    /// <summary>Posts an action to the UI thread (status, prompts, tray); null for <c>Dispatcher.UIThread.Post</c>.</summary>
    public Action<Action>? Marshal { get; init; }

    public static SyncModuleOptions Default { get; } = new();
}
