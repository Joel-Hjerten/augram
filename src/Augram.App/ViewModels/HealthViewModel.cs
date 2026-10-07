using System.Globalization;
using Augram.Core.Abstractions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The health summary (N4) as display strings, re-read from <see cref="IHealthSource"/> every
/// <c>interval</c> on the UI thread. A zero interval means refresh on demand only (tests).
/// </summary>
public sealed class HealthViewModel : ObservableObject, IDisposable
{
    private const string Unknown = "–";
    private readonly IHealthSource _source;
    private readonly DispatcherTimer? _timer;

    public HealthViewModel(IHealthSource source, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Refresh();
        if (interval > TimeSpan.Zero)
        {
            _timer = new DispatcherTimer(interval, DispatcherPriority.Background, (_, _) => Refresh());
            _timer.Start();
        }
    }

    public string HookAlive { get; private set => SetProperty(ref field, value); } = Unknown;

    public string HookReinstalls { get; private set => SetProperty(ref field, value); } = Unknown;

    public string EventsLastMinute { get; private set => SetProperty(ref field, value); } = Unknown;

    public string LastStrokeLatency { get; private set => SetProperty(ref field, value); } = Unknown;

    public string LastActivation { get; private set => SetProperty(ref field, value); } = Unknown;

    public string OverlayFirstFrame { get; private set => SetProperty(ref field, value); } = Unknown;

    public string Uptime { get; private set => SetProperty(ref field, value); } = Unknown;

    public string Memory { get; private set => SetProperty(ref field, value); } = Unknown;

    /// <summary>"UpToDate at 14:32:05", "Failed at 14:32:05"; "–" before the first sync or with sync off.</summary>
    public string LastSync { get; private set => SetProperty(ref field, value); } = Unknown;

    public void Refresh()
    {
        var s = _source.Current();
        HookAlive = s.HookAliveSince is { } since ? "since " + since.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "not installed";
        HookReinstalls = Number(s.HookReinstallCount);
        EventsLastMinute = Number(s.EventsLastMinute);
        LastStrokeLatency = Millis(s.LastStrokeLatencyMs);
        LastActivation = s.LastActivationOutcome ?? Unknown;
        OverlayFirstFrame = Millis(s.OverlayFirstFrameMs);
        Uptime = s.UptimeSeconds is { } seconds ? TimeSpan.FromSeconds(seconds).ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture) : Unknown;
        Memory = s.WorkingSetBytes is { } bytes ? (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : Unknown;
        LastSync = s.LastSyncOutcome is { } outcome
            ? s.LastSyncAt is { } at ? $"{outcome} at {at.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)}" : outcome
            : Unknown;
    }

    public void Dispose() => _timer?.Stop();

    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? Unknown;

    private static string Millis(double? value) => value is { } ms ? ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : Unknown;
}
