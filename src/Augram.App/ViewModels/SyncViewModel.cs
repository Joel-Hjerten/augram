using Augram.App.Hosting;
using Augram.App.Sync;
using Augram.Core.Config;
using Augram.Core.Sync;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// Options › Sync (F8 sync): a projection over <see cref="SettingsStore"/> (the sync section) and
/// <see cref="SyncService"/> (status, conflicts). The repository address and the machine name are each a
/// <see cref="DraftText"/>: each keystroke re-checks the draft (<see cref="SyncSettingsRules.UrlProblem"/>, a blank
/// name) and shows the problem inline; leaving the field or pressing Enter commits a valid draft through
/// <see cref="SettingsStore.SetSync"/> (one store change, so a URL is never applied half-typed: a new URL resets the
/// sync state), Escape reverts it.
/// Automatic sync is a toggle, one store change per click. The status line, the conflicts line (with Resolve…) and
/// the details (repairs and notes of the last run) follow the service, whose <c>Changed</c> arrives on the sync
/// worker and is marshalled here.
/// </summary>
public sealed class SyncViewModel : ObservableObject, IDisposable
{
    public const string NameProblemText = "This machine needs a name for sync.";

    private readonly SettingsStore _settings;
    private readonly SyncService _service;
    private readonly ISyncConflictPresenter _conflicts;
    private readonly Action<Action> _marshal;

    /// <remarks><c>marshal</c> runs the action on the UI thread; null is <see cref="UiThread.Post"/>.</remarks>
    public SyncViewModel(SettingsStore settings, SyncService service, ISyncConflictPresenter conflicts, Action<Action>? marshal = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _conflicts = conflicts ?? throw new ArgumentNullException(nameof(conflicts));
        _marshal = marshal ?? UiThread.Post;
        RepositoryUrl = new DraftText(
            () => Current.RepositoryUrl,
            text => string.IsNullOrWhiteSpace(text) ? null : SyncSettingsRules.UrlProblem(text),
            url => Apply(Current with { RepositoryUrl = url }));
        MachineName = new DraftText(
            () => Current.MachineName,
            text => string.IsNullOrWhiteSpace(text) ? NameProblemText : null,
            name => Apply(Current with { MachineName = name! })); // never null: a blank name is a problem
        _settings.Changed += OnSettingsChanged;
        _service.Changed += OnServiceChanged;
    }

    private SyncSettings Current => _settings.Current.Sync;

    /// <summary>The repository address as typed; blank turns sync off once committed.</summary>
    public DraftText RepositoryUrl { get; }

    /// <summary>This machine's name as typed; it cannot be blank.</summary>
    public DraftText MachineName { get; }

    public bool AutoSync
    {
        get => Current.AutoSync;
        set
        {
            if (value != Current.AutoSync)
            {
                Apply(Current with { AutoSync = value });
            }
        }
    }

    public bool IsConfigured => Current.IsOn;

    public bool IsRunning => _service.IsRunning;

    public bool CanSyncNow => IsConfigured && !IsRunning;

    /// <summary>The status line (<see cref="SyncStatusText.Status"/>).</summary>
    public string StatusText => SyncStatusText.Status(_service.Snapshot);

    public IReadOnlyList<SyncConflict> Conflicts => _service.PendingConflicts();

    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>"2 conflicts with Mac".</summary>
    public string ConflictsText => SyncConflictsViewModel.Describe(Conflicts);

    /// <summary>The last run's repairs and notes, one per line ("Incoming gesture 'Zig' renamed 'Zig (2)'…").</summary>
    public string DetailsText => _service.LastReport is { } report
        ? string.Join(Environment.NewLine, report.Repairs.Select(repair => repair.Description).Concat(report.Notes))
        : string.Empty;

    public bool HasDetails => DetailsText.Length > 0;

    public void SyncNow() => _service.SyncNow();

    /// <summary>Resolve…: shows the pending conflicts; the choices go to the service, which resolves them on the worker and then syncs.</summary>
    public async Task ResolveConflictsAsync()
    {
        var conflicts = Conflicts;
        if (conflicts.Count == 0)
        {
            return;
        }

        if (await _conflicts.ResolveAsync(conflicts).ConfigureAwait(true) is { Count: > 0 } resolutions)
        {
            _service.Resolve(resolutions);
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _service.Changed -= OnServiceChanged;
    }

    private void Apply(SyncSettings sync)
    {
        try
        {
            _settings.SetSync(sync);
        }
        catch (SettingsValidationException exception)
        {
            // The checks above mirror the rules; this is the store's own word if they ever drift apart.
            RepositoryUrl.Problem = exception.Message;
        }
    }

    // UI thread. The drafts follow the store (one being typed stays); the toggle and the status re-read it.
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        RepositoryUrl.Follow();
        MachineName.Follow();
        OnPropertyChanged(nameof(AutoSync));
        RaiseStatus();
    }

    // Sync worker.
    private void OnServiceChanged(object? sender, EventArgs e) => _marshal(RaiseStatus);

    private void RaiseStatus()
    {
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanSyncNow));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Conflicts));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(ConflictsText));
        OnPropertyChanged(nameof(DetailsText));
        OnPropertyChanged(nameof(HasDetails));
    }
}
