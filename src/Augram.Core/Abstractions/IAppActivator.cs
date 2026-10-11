namespace Augram.Core.Abstractions;

/// <summary>
/// Brings a running app to the front (the Open app step, Joel 2026-10-09): its topmost window, restored when minimized.
/// Platform.Windows finds it among the top-level windows by executable file name; on macOS <see cref="NullAppActivator"/>
/// answers "not running" and the step launches instead, because <c>open -a</c> brings a running app forward by itself.
/// Called on the command executor thread only. Nothing here throws for an expected failure.
/// </summary>
public interface IAppActivator
{
    AppActivation BringToFront(string executable);
}
