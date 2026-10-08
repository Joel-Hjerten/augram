namespace Augram.Core.Abstractions;

/// <summary>
/// Starts a program, document, folder or URI (ADR-0002 §2; the Run step, A4). Implemented by Platform.Windows (shell
/// execute, UAC elevation) and Platform.MacOS (<c>open</c>, or the executable itself); <see cref="NullProcessLauncher"/>
/// declines. Called on the command executor thread only, never from a hook handler or the UI thread. It never waits for
/// the started program to exit and returns within a bounded time: an adapter that has to wait on the OS (a UAC prompt, a
/// slow network path) reports <see cref="ProcessLaunchOutcome.Started"/> with a note once its wait runs out and logs the
/// late outcome itself. Nothing here throws for an expected failure; it comes back as the result.
/// </summary>
public interface IProcessLauncher
{
    ProcessLaunchResult Launch(ProcessLaunch launch);
}
