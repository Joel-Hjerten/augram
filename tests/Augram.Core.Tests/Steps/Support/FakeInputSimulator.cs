using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>
/// Fake <see cref="IInputSimulator"/>: records every call as one line ("press VolumeUp", "release VolumeUp",
/// "hotkey Control+W", "click Left@10,20", "text hi") in order, with a configurable answer per call kind.
/// </summary>
internal sealed class FakeInputSimulator : IInputSimulator
{
    private readonly List<string> _calls = [];

    public SimulationResult PressResult { get; set; } = SimulationResult.Success;

    public SimulationResult ReleaseResult { get; set; } = SimulationResult.Success;

    public SimulationResult OtherResult { get; set; } = SimulationResult.Success;

    public IReadOnlyList<string> Calls => _calls;

    public SimulationResult Click(MouseButton button, int x, int y) => Record($"click {button}@{x},{y}", OtherResult);

    public SimulationResult KeyPress(KeyCode key) => Record($"press {key}", PressResult);

    public SimulationResult KeyRelease(KeyCode key) => Record($"release {key}", ReleaseResult);

    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key) => Record($"hotkey {modifiers}+{key}", OtherResult);

    public SimulationResult TypeText(string text) => Record($"text {text}", OtherResult);

    public SimulationResult TypeTextByKeys(string text) => Record($"keys {text}", OtherResult);

    private SimulationResult Record(string call, SimulationResult result)
    {
        _calls.Add(call);
        return result;
    }
}
