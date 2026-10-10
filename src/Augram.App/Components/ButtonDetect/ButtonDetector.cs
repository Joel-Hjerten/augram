using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MouseButton = Augram.Core.Capture.MouseButton;

namespace Augram.App.Components.ButtonDetect;

/// <summary>
/// One detect-to-assign at a time for a form or header (F1; a hold remap command's buttons, plan 0002 step 4): <see cref="Start"/>
/// listens for the next mouse button press and reports it once to <c>detected</c>, then stops. It asks the engine
/// (<see cref="IButtonCapture"/>: <see cref="Capture"/>, else the application resource <see cref="ButtonCaptureResourceKey"/>),
/// which sees a press anywhere, the stroke button included, and also takes a press on the owner's own window (the only
/// source under <c>--no-engine</c>); that press is marked handled, so answering never also clicks what is under the pointer.
/// It gives up after <see cref="Timeout"/> and says so. <see cref="Status"/> is the line to show; <see cref="Changed"/> says
/// when it or <see cref="IsListening"/> moved. UI thread only.
/// </summary>
public sealed class ButtonDetector : IDisposable
{
    public const string ButtonCaptureResourceKey = "Augram.ButtonCapture";
    public const string ListeningText = "Press the mouse button to add (anywhere)…";
    public const string WindowOnlyText = "Press the mouse button over this window: the engine is off, so presses elsewhere are not seen…";
    public const string TimedOutText = "No button seen in 5 seconds. If you pressed one, its vendor software may consume it before Augram can see it.";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly Control _owner;
    private readonly Action<MouseButton> _detected;
    private IDisposable? _engine;
    private TopLevel? _topLevel;
    private DispatcherTimer? _timer;
    private int _session;

    public ButtonDetector(Control owner, Action<MouseButton> detected)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(detected);
        _owner = owner;
        _detected = detected;
    }

    /// <summary>Raised when <see cref="IsListening"/> or <see cref="Status"/> changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Explicit capture service (tests, gallery); null looks up <see cref="ButtonCaptureResourceKey"/>.</summary>
    public IButtonCapture? Capture { get; set; }

    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    public bool IsListening { get; private set; }

    /// <summary>What to say beside the button: the prompt while listening, the timeout, or nothing.</summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>Starts listening (again); does nothing before the owner is in a window.</summary>
    public void Start()
    {
        End(string.Empty);
        if (TopLevel.GetTopLevel(_owner) is not { } topLevel)
        {
            return;
        }

        var session = ++_session;
        var service = Capture ?? (_owner.TryFindResource(ButtonCaptureResourceKey, out var found) ? found as IButtonCapture : null);
        _engine = service?.Begin(button => Detected(session, button));
        _topLevel = topLevel;
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _timer = new DispatcherTimer { Interval = Timeout };
        _timer.Tick += (_, _) =>
        {
            if (session == _session)
            {
                End(TimedOutText);
            }
        };
        _timer.Start();
        IsListening = true;
        SetStatus(_engine is null ? WindowOnlyText : ListeningText);
    }

    /// <summary>Stops listening and clears the status.</summary>
    public void Stop() => End(string.Empty);

    public void Dispose() => End(string.Empty);

    /// <summary>The Core button an Avalonia press is for; null for a press that is not a button going down.</summary>
    public static MouseButton? ToButton(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
        PointerUpdateKind.RightButtonPressed => MouseButton.Right,
        PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
        PointerUpdateKind.XButton1Pressed => MouseButton.X1,
        PointerUpdateKind.XButton2Pressed => MouseButton.X2,
        _ => null,
    };

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (ToButton(e.GetCurrentPoint(null).Properties.PointerUpdateKind) is { } button)
        {
            Detected(_session, button);
        }
    }

    private void Detected(int session, MouseButton button)
    {
        if (session != _session || !IsListening)
        {
            return;
        }

        End(string.Empty);
        _detected(button);
    }

    private void End(string status)
    {
        var wasListening = IsListening;
        _session++;
        _engine?.Dispose();
        _engine = null;
        _topLevel?.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _topLevel = null;
        _timer?.Stop();
        _timer = null;
        IsListening = false;
        if (wasListening || Status != status)
        {
            SetStatus(status);
        }
    }

    private void SetStatus(string status)
    {
        Status = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
