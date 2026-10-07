using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Engine.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting;

/// <summary>
/// Detect-to-assign for the stroke button (F1): <see cref="Start"/> asks the engine for the next
/// physical press, writes it to the settings store, and gives up after <c>timeout</c>. The engine
/// reports on its worker thread and the timer on a pool thread; both are marshalled to the writer
/// thread, where <see cref="IsListening"/> decides which one wins. A press that vendor software
/// consumes at driver level never reaches the hook, so the timeout is what the user sees then.
/// </summary>
public sealed class StrokeButtonDetection : ObservableObject, IDisposable
{
    public const string ListeningText = "Press the button you want to use…";
    public const string TimedOutText = "No button seen in 5 seconds. If you pressed one, its vendor software may consume it before Augram can see it.";
    public const string EngineOffText = "Detect needs the engine, which is off in this run (--no-engine). Pick the button above.";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly EngineHost _host;
    private readonly SettingsStore _settings;
    private readonly Action<Action> _marshal;
    private readonly TimeSpan _timeout;
    private IDisposable? _capture;
    private Timer? _timer;

    public StrokeButtonDetection(EngineHost host, SettingsStore settings, Action<Action> marshal, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(marshal);
        _host = host;
        _settings = settings;
        _marshal = marshal;
        _timeout = timeout ?? DefaultTimeout;
    }

    public bool IsListening { get; private set => SetProperty(ref field, value); }

    /// <summary>What the Options page shows beside the button: empty, the prompt, the result, or the timeout.</summary>
    public string Status { get; private set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>Writer thread. A second call while listening restarts the timeout. Without a running engine nothing can be seen, so it says so at once.</summary>
    public void Start()
    {
        Stop();
        if (!_host.IsRunning)
        {
            Status = EngineOffText;
            return;
        }

        IsListening = true;
        Status = ListeningText;
        _capture = _host.CaptureNextButtonPress(button => _marshal(() => Assign(button)));
        _timer = new Timer(_ => _marshal(TimedOut), null, _timeout, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => Stop();

    private void Assign(MouseButton button)
    {
        if (!IsListening)
        {
            return;
        }

        // Status before Stop(): a reader that waits for IsListening to clear must then see the final text, not the prompt.
        var general = _settings.Current.General;
        if (general.StrokeButton != button)
        {
            _settings.SetGeneral(general with { StrokeButton = button });
        }

        Status = $"Stroke button set to {button}.";
        Stop();
    }

    private void TimedOut()
    {
        if (!IsListening)
        {
            return;
        }

        Status = TimedOutText;
        Stop();
    }

    private void Stop()
    {
        IsListening = false;
        _capture?.Dispose();
        _capture = null;
        _timer?.Dispose();
        _timer = null;
    }
}
