using Augram.Core.Capture;

namespace Augram.App.Components.ButtonDetect;

/// <summary>
/// Detect-to-assign for a mouse button (F1), as the App's forms see it: the next physical press anywhere, the stroke button
/// included, reported once to <c>onPress</c> on the UI thread; the press itself is handled as it would be otherwise (observed,
/// never swallowed). Disposing the handle cancels. <see cref="Begin"/> returns null when nothing can detect (the engine is
/// off: <c>--no-engine</c>), and <see cref="ButtonDetector"/> falls back to presses on its own window. Implemented by
/// <see cref="EngineButtonCapture"/>; faked in tests.
/// </summary>
public interface IButtonCapture
{
    IDisposable? Begin(Action<MouseButton> onPress);
}
