using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.App.Tests.Support;

/// <summary>Accepts every injection and records clicks; thread-safe because the engine worker calls it.</summary>
internal sealed class FakeInputSimulator : IInputSimulator
{
    private readonly object _gate = new();
    private readonly List<(MouseButton Button, int X, int Y)> _clicks = [];

    public IReadOnlyList<(MouseButton Button, int X, int Y)> Clicks
    {
        get
        {
            lock (_gate)
            {
                return [.. _clicks];
            }
        }
    }

    public SimulationResult Click(MouseButton button, int x, int y)
    {
        lock (_gate)
        {
            _clicks.Add((button, x, y));
        }

        return SimulationResult.Success;
    }

    public SimulationResult Press(MouseButton button, int x, int y) => SimulationResult.Success;

    public SimulationResult Release(MouseButton button) => SimulationResult.Success;

    public SimulationResult MoveTo(int x, int y) => SimulationResult.Success;

    public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y) => SimulationResult.Success;

    public SimulationResult KeyPress(KeyCode key) => SimulationResult.Success;

    public SimulationResult KeyRelease(KeyCode key) => SimulationResult.Success;

    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None) => SimulationResult.Success;

    public SimulationResult TypeText(string text) => SimulationResult.Success;

    public SimulationResult TypeTextByKeys(string text) => SimulationResult.Success;
}
