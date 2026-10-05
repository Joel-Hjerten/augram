namespace Augram.Core.Abstractions;

/// <summary>
/// Reads the pointer position without the hook (<c>GetCursorPos</c> on Windows). The hook
/// watchdog compares it across polls: a cursor that moves while no hook event arrives means the
/// hook is dead. Platform implements it; without one the watchdog relies on the hook's own signals.
/// </summary>
public interface ICursorProbe
{
    bool TryGetPosition(out int x, out int y);
}
