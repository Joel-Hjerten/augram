namespace Augram.Core.Abstractions;

/// <summary>
/// The global input hook as Core sees it (ADR-0002 §2): physical mouse and keyboard events in,
/// a synchronous suppress decision out. The Engine implements it over SharpHook; tests use a
/// fake that calls the handler directly. <see cref="Start"/> installs the hook on a thread the
/// source owns and invokes the handler there for every event; <see cref="Stop"/>
/// tears it down and waits for the thread. <see cref="HookHealthChanged"/> may be raised on the
/// hook thread itself (a hook loop failure), so a listener must not call <see cref="Stop"/>
/// from inside it; it defers to its own thread.
/// </summary>
public interface IInputSource : IDisposable
{
    bool IsRunning { get; }

    void Start(InputHandler handler);

    void Stop();

    event EventHandler<HookHealth>? HookHealthChanged;
}
