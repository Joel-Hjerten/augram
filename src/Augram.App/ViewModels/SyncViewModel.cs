using System.Globalization;
using Augram.App.Hosting;
using Augram.App.Sync;
using Augram.Core.Config;
using Augram.Core.Sync;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// Options › Sync (F8 sync): a projection over <see cref="SettingsStore"/> (the sync section) and
/// <see cref="SyncService"/> (status, conflicts). The repository address and the machine name are drafts while being
/// typed: each keystroke re-checks the draft (<see cref="SyncSettingsRules.UrlProblem"/>, a blank name) and shows the
/// problem inline; leaving the field or pressing Enter commits a valid draft through <see cref="SettingsStore.SetSync"/>
/// (one store change, so a URL is never applied half-typed: a new URL resets the sync state), Escape reverts it.
/// Automatic sync is a toggle, one store change per click. The status line, the conflicts line (with Resolve…) and
/// the details (repairs and notes of the last run) follow the service, whose <c>Changed</c> arrives on the sync
/// worker and is marshalled here.
/// </summary>
public sealed class SyncViewModel : ObservableObject, IDisposable
{
    public const string OffText = "Off: paste a repository address above to sync gestures and commands with your other machines.";
    public const string RunningText = "Syncing…";
    public const string PausedText = "Paused: choose how to join (Sync now asks again).";
    public const string NeverText = "Not synced yet.";
    public const string NameProblemText = "This machine needs a name for sync.";

    private readonly SettingsStore _settings;
    private readonly SyncService _service;
    private readonly ISyncConflictPresenter _conflicts;
    private readonly Action<Action> _marshal;
    private string? _urlSeen;
    private string _nameSeen;
    private string _urlText;
    private string _nameText;

    /// <remarks><c>marshal</c> runs the action on the UI thread; null is <c>Dispatcher.UIThread.Post</c>.</remarks>
    public SyncViewModel(SettingsStore settings, SyncService service, ISyncConflictPresenter conflicts, Action<Action>? marshal = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _conflicts = conflicts ?? throw new ArgumentNullException(nameof(conflicts));
        _marshal = marshal ?? (action => Dispatcher.UIThread.Post(action));
        _urlSeen = Current.RepositoryUrl;
        _nameSeen = Current.MachineName;
        _urlText = _urlSeen ?? string.Empty;
        _nameText = _nameSeen;
        _settings.Changed += OnSettingsChanged;
        _service.Changed += OnServiceChanged;
    }

    private SyncSettings Current => _settings.Current.Sync;

    /// <summary>The address as typed; committed by <see cref="CommitRepositoryUrl"/>.</summary>
    public string RepositoryUrlText
    {
        get => _urlText;
        set
        {
            if (SetProperty(ref _urlText, value ?? string.Empty))
            {
                UrlProblem = string.IsNullOrWhiteSpace(_urlText) ? null : SyncSettingsRules.UrlProblem(_urlText);
            }
        }
    }

    /// <summary>Why the typed address cannot be used, or null.</summary>
    public string? UrlProblem
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(UrlProblemText));
            }
        }
    }

    public string UrlProblemText => UrlProblem ?? string.Empty;

    /// <summary>This machine's name as typed; committed by <see cref="CommitMachineName"/>.</summary>
    public string MachineNameText
    {
        get => _nameText;
        set
        {
            if (SetProperty(ref _nameText, value ?? string.Empty))
            {
                MachineNameProblem = string.IsNullOrWhiteSpace(_nameText) ? NameProblemText : null;
            }
        }
    }

    public string? MachineNameProblem
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(MachineNameProblemText));
            }
        }
    }

    public string MachineNameProblemText => MachineNameProblem ?? string.Empty;

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

    public string StatusText => Status(IsConfigured, _service.IsRunning, _service.IsPaused, _service.LastReport, _service.OtherMachines);

    public IReadOnlyList<SyncConflict> Conflicts => _service.PendingConflicts();

    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>"2 conflicts with Mac".</summary>
    public string ConflictsText => SyncConflictsViewModel.Describe(Conflicts);

    /// <summary>The last run's repairs and notes, one per line ("Incoming gesture 'Zig' renamed 'Zig (2)'…").</summary>
    public string DetailsText => _service.LastReport is { } report
        ? string.Join(Environment.NewLine, report.Repairs.Select(repair => repair.Description).Concat(report.Notes))
        : string.Empty;

    public bool HasDetails => DetailsText.Length > 0;

    /// <summary>Applies the typed address when it is valid and different; blank turns sync off. A problem stays on show and nothing changes.</summary>
    public void CommitRepositoryUrl()
    {
        var text = _urlText.Trim();
        var url = text.Length == 0 ? null : text;
        if (url is not null && SyncSettingsRules.UrlProblem(url) is { } problem)
        {
            UrlProblem = problem;
            return;
        }

        UrlProblem = null;
        if (!string.Equals(url, Current.RepositoryUrl, StringComparison.Ordinal))
        {
            Apply(Current with { RepositoryUrl = url });
        }
    }

    public void RevertRepositoryUrl()
    {
        RepositoryUrlText = Current.RepositoryUrl ?? string.Empty;
        UrlProblem = null;
    }

    /// <summary>Applies the typed name when it is not blank and different.</summary>
    public void CommitMachineName()
    {
        var name = _nameText.Trim();
        if (name.Length == 0)
        {
            MachineNameProblem = NameProblemText;
            return;
        }

        MachineNameProblem = null;
        if (!string.Equals(name, Current.MachineName, StringComparison.Ordinal))
        {
            Apply(Current with { MachineName = name });
        }
    }

    public void RevertMachineName()
    {
        MachineNameText = Current.MachineName;
        MachineNameProblem = null;
    }

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

    /// <summary>The status line for the given state (pure, for tests and the tray).</summary>
    public static string Status(bool configured, bool running, bool paused, SyncReport? report, IReadOnlyList<string> otherMachines)
    {
        ArgumentNullException.ThrowIfNull(otherMachines);
        if (!configured)
        {
            return OffText;
        }

        if (running)
        {
            return RunningText;
        }

        if (report?.Status == SyncStatus.NeedsUpdate)
        {
            return NeedsUpdateText(report.NewerMachines);
        }

        if (paused || report?.Status == SyncStatus.NeedsJoinChoice)
        {
            return PausedText;
        }

        if (report is null || report.Status == SyncStatus.Off)
        {
            return NeverText;
        }

        var at = report.When.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        return report.Status switch
        {
            SyncStatus.UpToDate => $"Up to date (last synced {at}).",
            SyncStatus.Applied => $"Last synced {at}: {Changes(report.Counts)}{From(otherMachines)}.",
            _ => $"Sync failed at {at}: {report.Error ?? "unknown error"}",
        };
    }

    /// <summary>"Paused: Mac uses a newer Augram. Update this machine (pull, rebuild, restart) to resume."</summary>
    public static string NeedsUpdateText(IReadOnlyList<SyncNewerMachine> machines)
        => $"Paused: {SyncNewerMachine.Summary(machines)}. Update this machine (pull, rebuild, restart) to resume.";

    /// <summary>"3 commands changed", "1 gesture, 2 groups and 3 commands changed".</summary>
    public static string Changes(SyncCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var parts = new List<string>();
        Add(parts, counts.Gestures.Total, "gesture");
        Add(parts, counts.Groups.Total, "app group");
        Add(parts, counts.Categories.Total, "category", "categories");
        Add(parts, counts.HoldRemaps.Total, "hold remap");
        Add(parts, counts.Commands.Total, "command");
        Add(parts, counts.Ignored.Total, "excluded app");
        if (parts.Count == 0)
        {
            return "nothing changed";
        }

        var list = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
        return list + " changed";
    }

    private static void Add(List<string> parts, int count, string singular, string? plural = null)
    {
        if (count > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? singular : plural ?? singular + "s")}"));
        }
    }

    private static string From(IReadOnlyList<string> machines) => machines.Count == 0 ? string.Empty : " from " + string.Join(" and ", machines);

    private void Apply(SyncSettings sync)
    {
        try
        {
            _settings.SetSync(sync);
        }
        catch (SettingsValidationException exception)
        {
            // The checks above mirror the rules; this is the store's own word if they ever drift apart.
            UrlProblem = exception.Message;
        }
    }

    // UI thread. A stored value that changed elsewhere (or was just committed) replaces the draft; one being typed stays.
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        var sync = Current;
        if (!string.Equals(sync.RepositoryUrl, _urlSeen, StringComparison.Ordinal))
        {
            _urlSeen = sync.RepositoryUrl;
            RepositoryUrlText = sync.RepositoryUrl ?? string.Empty;
            UrlProblem = null;
        }

        if (!string.Equals(sync.MachineName, _nameSeen, StringComparison.Ordinal))
        {
            _nameSeen = sync.MachineName;
            MachineNameText = sync.MachineName;
            MachineNameProblem = null;
        }

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
