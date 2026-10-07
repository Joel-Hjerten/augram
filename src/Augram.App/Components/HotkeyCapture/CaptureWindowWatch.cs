using Augram.Engine.Hosting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// What a capturing <see cref="HotkeyCaptureBox"/> listens to on its window, attached for one capture and
/// disposed when it ends: any pointer press outside the field and the window losing activation (both mean
/// "leave", F5's mouse-only commit), and, only in the no-engine fallback, the window's own key events,
/// marked handled so Tab, Space or Enter cannot also move focus or press a button.
/// </summary>
internal sealed class CaptureWindowWatch : IDisposable
{
    private readonly TopLevel _topLevel;
    private readonly Visual _field;
    private readonly bool _readKeys;
    private readonly Action<KeyCaptureEvent> _onKey;
    private readonly Action _onLeave;

    public CaptureWindowWatch(TopLevel topLevel, Visual field, bool readKeys, Action<KeyCaptureEvent> onKey, Action onLeave)
    {
        _topLevel = topLevel;
        _field = field;
        _readKeys = readKeys;
        _onKey = onKey;
        _onLeave = onLeave;
        if (readKeys)
        {
            topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            topLevel.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (topLevel is WindowBase window)
        {
            window.Deactivated += OnDeactivated;
        }
    }

    public void Dispose()
    {
        if (_readKeys)
        {
            _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _topLevel.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        }

        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        if (_topLevel is WindowBase window)
        {
            window.Deactivated -= OnDeactivated;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        _onKey(KeyCaptureEvent.KeyDown(AvaloniaKeyMap.ToKeyCode(e.PhysicalKey), AvaloniaKeyMap.ToModifiers(e.KeyModifiers)));
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        _onKey(KeyCaptureEvent.KeyUp(AvaloniaKeyMap.ToKeyCode(e.PhysicalKey), AvaloniaKeyMap.ToModifiers(e.KeyModifiers)));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (source == _field || _field.IsVisualAncestorOf(source)))
        {
            return;
        }

        _onLeave();
    }

    private void OnDeactivated(object? sender, EventArgs e) => _onLeave();
}
