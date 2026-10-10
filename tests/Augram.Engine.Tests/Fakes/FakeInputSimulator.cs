using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Engine.Tests.Fakes;

/// <summary>
/// Records every injection; thread-safe because the worker and the executor call it. Clicks are in <see cref="Clicks"/>,
/// keys and text in <see cref="Keys"/>, and every mouse injection (clicks included) in order in <see cref="Mouse"/>
/// ("click Right@10,20", "down Right@10,20", "move 50,20", "scroll Down x1@10,20", "up Right"), so a test can check that every injected down got
/// its up (A19). <see cref="All"/> has both in the one order they were made (hold remaps interleave keys and buttons).
/// <see cref="BlockOn"/> holds the calling thread inside the injection of that entry until <see cref="Unblock"/>, so a test
/// can keep the worker busy while the hook decides.
/// </summary>
internal sealed class FakeInputSimulator : IInputSimulator
{
    private readonly object _gate = new();
    private readonly List<(MouseButton Button, int X, int Y)> _clicks = [];
    private readonly List<string> _keys = [];
    private readonly List<string> _mouse = [];
    private readonly List<string> _all = [];
    private readonly ManualResetEventSlim _released = new(true);
    private string? _blockOn;

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

    public IReadOnlyList<string> Mouse
    {
        get
        {
            lock (_gate)
            {
                return [.. _mouse];
            }
        }
    }

    /// <summary>Every injection, keys and mouse together, in order.</summary>
    public IReadOnlyList<string> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _all];
            }
        }
    }

    /// <summary>The next injection recorded as this entry ("press Space") waits, after recording, until <see cref="Unblock"/>.</summary>
    public string? BlockOn
    {
        get => Volatile.Read(ref _blockOn);
        set
        {
            _released.Reset();
            Volatile.Write(ref _blockOn, value);
        }
    }

    public void Unblock()
    {
        Volatile.Write(ref _blockOn, null);
        _released.Set();
    }

    public SimulationResult Click(MouseButton button, int x, int y)
    {
        lock (_gate)
        {
            _clicks.Add((button, x, y));
        }

        return RecordMouse($"click {button}@{x},{y}");
    }

    public SimulationResult Press(MouseButton button, int x, int y) => RecordMouse($"down {button}@{x},{y}");

    public SimulationResult Release(MouseButton button) => RecordMouse($"up {button}");

    public SimulationResult MoveTo(int x, int y) => RecordMouse($"move {x},{y}");

    public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y) => RecordMouse($"scroll {direction} x{notches}@{x},{y}");

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
            _all.Add(entry);
        }

        Wait(entry);
        return SimulationResult.Success;
    }

    private SimulationResult RecordMouse(string entry)
    {
        lock (_gate)
        {
            _mouse.Add(entry);
            _all.Add(entry);
        }

        Wait(entry);
        return SimulationResult.Success;
    }

    private void Wait(string entry)
    {
        if (entry == Volatile.Read(ref _blockOn))
        {
            _released.Wait(TimeSpan.FromSeconds(30));
        }
    }
}
