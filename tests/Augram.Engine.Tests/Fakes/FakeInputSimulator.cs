using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Engine.Tests.Fakes;

/// <summary>Records every injection; thread-safe because the worker calls it.</summary>
internal sealed class FakeInputSimulator : IInputSimulator
{
    private readonly object _gate = new();
    private readonly List<(MouseButton Button, int X, int Y)> _clicks = [];
    private readonly List<string> _keys = [];

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

    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_gate)
            {
                return [.. _keys];
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

    public SimulationResult KeyPress(KeyCode key) => Record($"press {key}");

    public SimulationResult KeyRelease(KeyCode key) => Record($"release {key}");

    /// <summary>"hotkey Control+W"; "hotkey Alt+F9 right Alt" when some modifiers are the right-hand key.</summary>
    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None) =>
        Record(rightHand == KeyModifiers.None ? $"hotkey {modifiers}+{key}" : $"hotkey {modifiers}+{key} right {rightHand}");

    public SimulationResult TypeText(string text) => Record($"text {text}");

    public SimulationResult TypeTextByKeys(string text) => Record($"keys {text}");

    private SimulationResult Record(string entry)
    {
        lock (_gate)
        {
            _keys.Add(entry);
        }

        return SimulationResult.Success;
    }
}
