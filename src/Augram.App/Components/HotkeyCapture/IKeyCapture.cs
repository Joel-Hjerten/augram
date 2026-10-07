using Augram.Engine.Hosting;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// System-wide key capture for the hotkey field (F5), as the App sees it: while the returned handle is
/// alive every key press goes to <c>onEvent</c> instead of the OS (Win+L, Alt+Tab, Esc, PrintScreen
/// included), on the UI thread; disposing it gives the keyboard back. The engine also ends it on its own
/// (idle watchdog after <c>idleTimeout</c>, hook reset, engine stop), reported as
/// <see cref="KeyCaptureEventKind.Released"/>. <see cref="Begin"/> returns null when nothing can capture
/// (the engine is off: <c>--no-engine</c>), and the field falls back to its own window's key events.
/// Implemented by <see cref="EngineKeyCapture"/>; faked in tests.
/// </summary>
public interface IKeyCapture
{
    IDisposable? Begin(Action<KeyCaptureEvent> onEvent, TimeSpan idleTimeout);
}
