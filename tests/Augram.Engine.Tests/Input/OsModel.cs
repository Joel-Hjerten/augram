using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Remap;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// What the OS believes is held: physical events the hook passed, plus what the worker injects, plus another program's posted
/// releases (plan 0005 decision 10). After such a release, a real release the hook passes may find the button already up: that
/// one is the other program's to swallow or not, so it is allowed once. Buttons are counted (plan 0015): a drag trigger's output
/// Left gets a second down when the physical Left, which passes during the drag, goes down too; the OS ends released as long as
/// every down has its up, which is what counts (as in <c>HoldPairingTests</c>).
/// </summary>
internal sealed class OsModel(int sequence)
{
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();

    private readonly int[] _buttons = new int[Buttons.Length];
    private readonly bool[] _releasedElsewhere = new bool[Buttons.Length];
    private readonly HashSet<KeyCode> _keys = [];

    // The button whose fired chord holds an output (plan 0005): the worker presses it at the fire and releases it at the end.
    private MouseButton? _chord;

    // What a fired drag holds (plan 0015), as the worker plays it: its owner, and the output (null: the owner handed back, or steps run).
    private (MouseButton Owner, RemapOutput.Button? Output, bool HandedBack)? _drag;

    public void Physical(RawInput raw, bool suppressed)
    {
        if (raw.Kind == RawInputKind.ButtonReleasedElsewhere)
        {
            Assert.False(suppressed, $"sequence {sequence}: a release elsewhere of {raw.Button} was suppressed");
            _buttons[(int)raw.Button] = 0;
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
                _buttons[(int)raw.Button]++;
                _releasedElsewhere[(int)raw.Button] = false;
                break;
            case RawInputKind.ButtonUp:
                Up(raw.Button, "an up");
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
                Assert.True(_buttons[(int)back.Button] == 0, $"sequence {sequence}: a hand-back of {back.Button}, which the OS already holds");
                _buttons[(int)back.Button]++;
                _releasedElsewhere[(int)back.Button] = false;
                break;
            case CaptureOutcome.ReleaseHandedBack release:
                Assert.True(_buttons[(int)release.Button] > 0, $"sequence {sequence}: a hand-back release of {release.Button}, which the OS does not hold");
                _buttons[(int)release.Button]--;
                break;
            case CaptureOutcome.ButtonTrigger fired:
                Assert.True(_chord is null, $"sequence {sequence}: {fired.Button} fired while the chord of {_chord} still holds its output");
                _chord = fired.Button;
                break;
            case CaptureOutcome.ButtonTriggerEnded ended:
                Assert.True(_chord == ended.Button, $"sequence {sequence}: the chord of {ended.Button} ended, but {_chord?.ToString() ?? "none"} holds an output");
                _chord = null;
                break;
        }
    }

    /// <summary>A drag trigger fired (plan 0015), played as the worker plays it: the output held (its held key, then its button), steps run (nothing held), or the owner handed back.</summary>
    public void DragFired(MouseButton owner, RemapOutput.Button? output, bool handedBack)
    {
        Assert.True(_drag is null, $"sequence {sequence}: a drag of {owner} fired while the drag of {_drag?.Owner} still holds");
        _drag = (owner, output, handedBack);
        if (output is not null)
        {
            if (output.HeldKey != KeyCode.None)
            {
                Assert.True(_keys.Add(output.HeldKey), $"sequence {sequence}: the held key {output.HeldKey} pressed while the OS holds it");
            }

            _buttons[(int)output.MouseButton]++;
            _releasedElsewhere[(int)output.MouseButton] = false;
        }
        else if (handedBack)
        {
            Assert.True(_buttons[(int)owner] == 0, $"sequence {sequence}: a drag's hand-back of {owner}, which the OS already holds");
            _buttons[(int)owner]++;
            _releasedElsewhere[(int)owner] = false;
        }
    }

    /// <summary>The drag of <paramref name="owner"/> ended: the output's button up, then its held key; a handed-back owner up unless another program released it.</summary>
    public void DragEnded(MouseButton owner, bool releasedElsewhere)
    {
        Assert.True(_drag?.Owner == owner, $"sequence {sequence}: the drag of {owner} ended, but {_drag?.Owner.ToString() ?? "none"} holds");
        var (_, output, handedBack) = _drag!.Value;
        _drag = null;
        if (output is not null)
        {
            Up(output.MouseButton, "the drag output's up");
            if (output.HeldKey != KeyCode.None)
            {
                Assert.True(_keys.Remove(output.HeldKey), $"sequence {sequence}: the held key {output.HeldKey} released, which the OS does not hold");
            }
        }
        else if (handedBack && !releasedElsewhere)
        {
            Up(owner, "a drag's hand-back release");
        }
    }

    public void AssertNothingHeld()
    {
        Assert.True(_chord is null, $"sequence {sequence}: the chord of {_chord} still holds its output");
        Assert.True(_drag is null, $"sequence {sequence}: the drag of {_drag?.Owner} still holds");
        Assert.True(_buttons.All(held => held == 0), $"sequence {sequence}: the OS still holds {string.Join(", ", Buttons.Where(b => _buttons[(int)b] != 0))}");
        Assert.True(_keys.Count == 0, $"sequence {sequence}: the OS still holds {string.Join(", ", _keys)}");
    }

    /// <summary>
    /// One up of <paramref name="button"/>. After another program's release it is up already, so the ups still owed to the downs
    /// it ended (the physical one, a drag output's of the same button) may each find it up, until the next down.
    /// </summary>
    private void Up(MouseButton button, string what)
    {
        if (_buttons[(int)button] > 0)
        {
            _buttons[(int)button]--;
            return;
        }

        Assert.True(_releasedElsewhere[(int)button], $"sequence {sequence}: the OS got {what} for {button}, which it does not hold");
    }
}
