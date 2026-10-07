using Augram.Core.Abstractions;

namespace Augram.Engine.Input;

/// <summary>
/// The hook thread's record of each key's press as the OS saw it, so hotkey capture (F5) can swallow
/// keys without ever leaving the OS with a key it thinks is held (A19, applied to keys). Per key: up,
/// <em>passed</em> (the OS saw the press) or <em>owed</em> (the press was suppressed, so its repeats and
/// its release must be too). A press is suppressed iff capture is armed when it starts; a repeat or a
/// release follows its press, whatever capture does in between: a key held from before capture began
/// reaches the OS to its release, and a key pressed during capture stays swallowed to its release even
/// after capture ended.
/// <para>
/// A held key repeats at least once a second (the slowest Windows typematic delay), so a state older
/// than <see cref="LostReleaseAfterMs"/> means the hook missed the release (secure desktop, session
/// switch) and the next press starts fresh. Mistaking a stalled repeat for a fresh press is safe both
/// ways: a press and its release are always decided alike. Single writer (the hook thread);
/// <see cref="Reset"/> may race it once after a reinstall, which costs one key's record at most.
/// </para>
/// </summary>
public sealed class KeySuppressionShadow
{
    public const long LostReleaseAfterMs = 2_000;

    private const int Slots = 256;
    private const byte Up = 0;
    private const byte Passed = 1;
    private const byte Owed = 2;

    private readonly byte[] _state = new byte[Slots];
    private readonly long[] _lastMs = new long[Slots];

    /// <summary>Keys whose suppressed press still owes a suppressed release; for tests and diagnostics.</summary>
    public int OwedCount => _state.Count(state => state == Owed);

    public bool IsOwed(KeyCode key) => Volatile.Read(ref _state[Slot(key)]) == Owed;

    /// <summary>Forget every key. Only when the hook was reinstalled or the OS state is otherwise unknown.</summary>
    public void Reset() => Array.Clear(_state);

    /// <summary>Decides for one key event and records it; anything but a key event is never suppressed.</summary>
    public bool Decide(in RawInput input, bool captureArmed)
    {
        if (input.Kind is not (RawInputKind.KeyDown or RawInputKind.KeyUp))
        {
            return false;
        }

        var slot = Slot(input.Key);
        var state = Volatile.Read(ref _state[slot]);
        if (state != Up && input.TimestampMs - _lastMs[slot] > LostReleaseAfterMs)
        {
            state = Up;
        }

        _lastMs[slot] = input.TimestampMs;
        if (input.Kind == RawInputKind.KeyUp)
        {
            Volatile.Write(ref _state[slot], Up);
            return state == Owed;
        }

        if (state == Up)
        {
            state = captureArmed ? Owed : Passed;
        }

        Volatile.Write(ref _state[slot], state);
        return state == Owed;
    }

    private static int Slot(KeyCode key) => (uint)key < Slots ? (int)key : 0;
}
