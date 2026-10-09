using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Augram.App.Components.WindowFinder;

/// <summary>
/// The drag-to-identify magnifier (F5 app identification; SP.net's crosshair on App Definition): press it with the left
/// button, drag onto any window on screen, release, and <see cref="Picked"/> names that window. While dragging the pointer
/// is captured, the cursor is a crosshair, <see cref="IsFinding"/> is true (<c>:finding</c>) and <see cref="HoverText"/>
/// says what is under the pointer (<see cref="Describe"/>: "chrome.exe · Google Chrome" by default), which the theme shows
/// beside the magnifier. Released over Augram's own windows, over nothing, or after Esc, a right-click or a lost capture,
/// nothing is picked; the release itself never reaches the window underneath. The windows come from
/// <see cref="IWindowSystem"/> (<see cref="WindowSystem"/>, else the application resource
/// <see cref="WindowSystemResourceKey"/> that <c>EngineModule.Start</c> publishes, else none) through
/// <see cref="WindowProbe"/>, on the UI thread: neither <c>WindowAt</c> nor <c>WindowKeyAt</c> asks another app anything.
/// <para>
/// Outside the window: Avalonia's pointer capture is the platform's (Windows: <c>SetCapture</c>, so moves and the release
/// keep arriving while the button is held anywhere on screen; macOS: AppKit sends the drag and the mouse-up to the window the
/// press was in). Positions arrive relative to this control, possibly far outside it, and <c>PointToScreen</c> turns them
/// into the window system's units: physical pixels on Windows (the app is per-monitor DPI aware, so the client position is
/// scaled back by the window's own scaling and offset by <c>ClientToScreen</c>, right on any monitor), points from the top
/// left of the main display on macOS (Avalonia.Native flips AppKit's bottom-left screen coordinates; CoreGraphics' window
/// list uses the same points).
/// </para>
/// </summary>
public sealed class WindowFinder : TemplatedControl
{
    public const string WindowSystemResourceKey = "Augram.WindowSystem";
    public const string PromptText = "Drag onto a window. Esc cancels.";
    public const string OwnWindowText = "Augram's own window: nothing to pick";
    public const string NoWindowText = "No window here";

    public static readonly StyledProperty<IWindowSystem?> WindowSystemProperty =
        AvaloniaProperty.Register<WindowFinder, IWindowSystem?>(nameof(WindowSystem));

    public static readonly StyledProperty<bool> IsFindingProperty =
        AvaloniaProperty.Register<WindowFinder, bool>(nameof(IsFinding));

    public static readonly StyledProperty<string> HoverTextProperty =
        AvaloniaProperty.Register<WindowFinder, string>(nameof(HoverText), PromptText);

    private WindowProbe? _probe;
    private TopLevel? _keys;
    private Cursor? _crosshair;

    /// <summary>Released over another app's window.</summary>
    public event EventHandler<WindowPickedEventArgs>? Picked;

    /// <summary>Explicit window system (tests, gallery); null looks up <see cref="WindowSystemResourceKey"/>.</summary>
    public IWindowSystem? WindowSystem { get => GetValue(WindowSystemProperty); set => SetValue(WindowSystemProperty, value); }

    public bool IsFinding { get => GetValue(IsFindingProperty); private set => SetValue(IsFindingProperty, value); }

    /// <summary>What is under the pointer while finding: <see cref="PromptText"/> until it moves, then a description.</summary>
    public string HoverText { get => GetValue(HoverTextProperty); private set => SetValue(HoverTextProperty, value); }

    /// <summary>How a window under the pointer reads: <see cref="Summary"/> unless a field's finder says what it would take ("C:\…\chrome.exe").</summary>
    public Func<WindowIdentity, string> Describe { get; set; } = Summary;

    /// <summary>"chrome.exe · Google Chrome", or the executable alone when the window has no title.</summary>
    public static string Summary(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return string.IsNullOrEmpty(window.Title) ? window.ProcessName : $"{window.ProcessName} · {window.Title}";
    }

    /// <summary>Ends a find without picking anything (Esc, a right-click, a lost capture, being unloaded).</summary>
    public void Cancel() => End();

    /// <summary>Starts finding: the pointer's own handlers call it on a left press; false while already finding or before the finder is in a window.</summary>
    internal bool BeginFind()
    {
        if (IsFinding || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return false;
        }

        var windows = WindowSystem
            ?? (this.TryFindResource(WindowSystemResourceKey, out var found) ? found as IWindowSystem : null)
            ?? NullWindowSystem.Instance;
        _probe = new WindowProbe(windows, Environment.ProcessId);
        _keys = topLevel;
        topLevel.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _crosshair ??= new Cursor(StandardCursorType.Cross);
        Cursor = _crosshair;
        HoverText = PromptText;
        IsFinding = true;
        PseudoClasses.Set(":finding", true);
        return true;
    }

    /// <summary>The pointer is at <paramref name="screen"/> (the window system's units): the feedback follows.</summary>
    internal void HoverAt(PixelPoint screen)
    {
        if (_probe is not { } probe)
        {
            return;
        }

        var window = probe.Hover(screen.X, screen.Y);
        HoverText = window is null ? NoWindowText : probe.IsOwn(window) ? OwnWindowText : Describe(window);
    }

    /// <summary>Released at <paramref name="screen"/>: the find ends, then <see cref="Picked"/> names another app's window there, if any.</summary>
    internal void ReleaseAt(PixelPoint screen)
    {
        if (_probe is not { } probe)
        {
            return;
        }

        var picked = probe.Pick(screen.X, screen.Y);
        End();
        if (picked is not null)
        {
            Picked?.Invoke(this, new WindowPickedEventArgs(picked));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsFinding)
        {
            // Another button while dragging cancels; the capture stays until the left release, which then picks nothing.
            End();
            e.Handled = true;
            return;
        }

        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed && BeginFind())
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsFinding)
        {
            HoverAt(ScreenPoint(e));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        if (IsFinding)
        {
            ReleaseAt(ScreenPoint(e));
        }

        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        End();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        End();
        _crosshair?.Dispose();
        _crosshair = null;
    }

    private PixelPoint ScreenPoint(PointerEventArgs e) => this.PointToScreen(e.GetPosition(this));

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsFinding)
        {
            End();
            e.Handled = true;
        }
    }

    /// <summary>The one exit: keys unhooked, the cursor back, the feedback closed.</summary>
    private void End()
    {
        if (!IsFinding)
        {
            return;
        }

        _probe = null;
        _keys?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _keys = null;
        ClearValue(CursorProperty);
        IsFinding = false;
        PseudoClasses.Set(":finding", false);
    }
}
