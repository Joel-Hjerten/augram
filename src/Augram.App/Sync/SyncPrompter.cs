using Augram.App.Hosting;
using Augram.Core.Sync;
using Avalonia.Threading;

namespace Augram.App.Sync;

/// <summary>
/// Asks the join question when a run answers <see cref="SyncStatus.NeedsJoinChoice"/> (F8 sync, join): after the
/// start-up run, after Sync now and after a new repository URL, since automatic runs are paused until then. One
/// dialog at a time, once per report. The answer runs <see cref="SyncService.Join"/>; Cancel leaves sync paused, and
/// the Options page says so. Listens on the sync worker and shows the dialog on the UI thread.
/// </summary>
public sealed class SyncPrompter : IDisposable
{
    private readonly SyncService _service;
    private readonly ISyncJoinPresenter _presenter;
    private readonly Action<Action> _marshal;
    private SyncReport? _prompted;
    private bool _asking;

    /// <remarks><c>marshal</c> runs the action on the UI thread; null is <c>Dispatcher.UIThread.Post</c>.</remarks>
    public SyncPrompter(SyncService service, ISyncJoinPresenter presenter, Action<Action>? marshal = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _marshal = marshal ?? (action => Dispatcher.UIThread.Post(action));
        _service.Changed += OnChanged;
    }

    /// <summary>The dialog's task while it is open (tests await it); completed otherwise.</summary>
    public Task Asking { get; private set; } = Task.CompletedTask;

    public void Dispose() => _service.Changed -= OnChanged;

    // Sync worker.
    private void OnChanged(object? sender, EventArgs e)
    {
        var report = _service.LastReport;
        if (report is not { Status: SyncStatus.NeedsJoinChoice } || _service.IsRunning || ReferenceEquals(report, _prompted))
        {
            return;
        }

        _prompted = report;
        _marshal(() => Asking = AskAsync(report));
    }

    // UI thread.
    private async Task AskAsync(SyncReport report)
    {
        if (_asking)
        {
            return;
        }

        _asking = true;
        try
        {
            if (await _presenter.ChooseAsync(report.OtherMachines).ConfigureAwait(true) is { } join)
            {
                _service.Join(join);
            }
        }
        finally
        {
            _asking = false;
        }
    }
}
