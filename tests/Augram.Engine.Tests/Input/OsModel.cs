using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// What the OS believes is held: physical events the hook passed, plus what the worker injects, plus another program's posted
/// releases (plan 0005 decision 10). After such a release, a real release the hook passes may find the button already up: that
/// one is the other program's to swallow or not, so it is allowed once.
/// </summary>
internal sealed class OsModel(int sequence)
{
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();

    private readonly bool[] _buttons = new bool[Buttons.Length];
    private readonly bool[] _releasedElsewhere = new bool[Buttons.Length];
    private readonly HashSet<KeyCode> _keys = [];

    public void Physical(RawInput raw, bool suppressed)
    {
        if (raw.Kind == RawInputKind.ButtonReleasedElsewhere)
        {
            Assert.False(suppressed, $"sequence {sequence}: a release elsewhere of {raw.Button} was suppressed");
            _buttons[(int)raw.Button] = false;
            _releasedElsewhere[(int)raw.Button] = true;
            return;
        }

        if (suppressed)
        {
            return;
        }

        switch (raw.Kind)
        {
            case RawInputKind.ButtonDown:
                _buttons[(int)raw.Button] = true;
                _releasedElsewhere[(int)raw.Button] = false;
                break;
            case RawInputKind.ButtonUp:
                Assert.True(_buttons[(int)raw.Button] || _releasedElsewhere[(int)raw.Button], $"sequence {sequence}: the OS got an up for {raw.Button}, which it does not hold");
                _buttons[(int)raw.Button] = false;
                _releasedElsewhere[(int)raw.Button] = false;
                break;
            case RawInputKind.KeyDown:
                _keys.Add(raw.Key);
                break;
            case RawInputKind.KeyUp:
                Assert.True(_keys.Remove(raw.Key), $"sequence {sequence}: the OS got an up for {raw.Key}, which it does not hold");
                break;
        }
    }

    public void Injected(CaptureOutcome outcome)
    {
        switch (outcome)
        {
            case CaptureOutcome.HandBack back:
                Assert.False(_buttons[(int)back.Button], $"sequence {sequence}: a hand-back of {back.Button}, which the OS already holds");
                _buttons[(int)back.Button] = true;
                _releasedElsewhere[(int)back.Button] = false;
                break;
            case CaptureOutcome.ReleaseHandedBack release:
                Assert.True(_buttons[(int)release.Button], $"sequence {sequence}: a hand-back release of {release.Button}, which the OS does not hold");
                _buttons[(int)release.Button] = false;
                break;
        }
    }

    public void AssertNothingHeld()
    {
        Assert.True(_buttons.All(held => !held), $"sequence {sequence}: the OS still holds {string.Join(", ", Buttons.Where(b => _buttons[(int)b]))}");
        Assert.True(_keys.Count == 0, $"sequence {sequence}: the OS still holds {string.Join(", ", _keys)}");
    }
}
