using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.Engine.Input;

/// <summary>
/// The hook thread's copy of the facts it needs to answer "suppress?" for hold remaps (F9, plan 0002) without running the
/// <see cref="HoldRemapMachine"/>, as <see cref="SuppressionShadow"/> is for gestures: the hold in progress (the
/// <see cref="HoldRemapEntry"/> claimed at the hold key's press, its time, whether it rolled over or something was used), the
/// input buttons whose consumed release is owed, the input keys claimed and the keys replayed. Every rule is the machine's
/// (the table in <c>Core/HoldRemaps/README.md</c>), so the hook's decision equals the one the worker's machine makes from the
/// same events (<c>HoldPairingTests</c> proves it over random sequences, and the worker cross-checks every live decision).
/// <para>
/// What the gate adds around it: the hold key's press is offered (<c>candidate</c>) only when the foreground's published plan
/// has the key, the engine is enabled and not paused by a focused app, the ignore key is up, no hotkey capture is armed, no
/// gesture press is owned and the press is fresh (<see cref="KeySuppressionShadow.IsFreshPress"/>). While nothing is
/// <see cref="Engaged"/> the machine would pass everything and change nothing, so no event is forwarded.
/// </para>
/// <para>
/// <b>Lost releases.</b> The keys this shadow owns (the hold key while held, claimed and replayed keys) never age: with key
/// repeat off (macOS) the hold key sends nothing between its press and its release, however long an orbit lasts, and that
/// release must still be swallowed and end the hold. A release really lost (secure desktop, lock) is followed by the hook
/// reset at unlock or resume, which forgets everything here; without one, the next press and release of the hold key end the
/// hold (its press is a repeat here).
/// </para>
/// <para>
/// <b>Focus moving away</b> (Joel, 2026-10-10). A hold remembers the app group whose plan it was claimed with; when the
/// published plan is another group's (or none) and nothing is owed, the gate ends the hold at its next event
/// (<see cref="FocusMovedFrom"/>, <see cref="EndByFocus"/>, the machine told by <see cref="HoldRemapEvent.FocusMoved"/>): no
/// tap, and the hold key stays swallowed to its release. With inputs owed it keeps following them.
/// </para>
/// <para>
/// <b>Order.</b> After a rollover (and at a predicted tap) the worker injects the hold key's tap and replays keys; a key
/// pressed before it has done so would reach the OS first ("a b" typed fast would come out "ab "). While any such message is
/// in flight (<see cref="PendingReplays"/>: the hook counts it up before posting, the worker down after injecting), a fresh
/// key press nobody else claims is claimed too and replayed by the worker in order (<see cref="HoldKeyVerdict.Ordered"/>);
/// its repeats and release follow it. Once the worker has caught up, keys pass through again.
/// </para>
/// Single writer (the hook thread), except <see cref="ReplayDone"/> (the worker) and <see cref="Reset"/> (after a hook
/// reinstall, which may race the hook once, as the other shadows' resets do).
/// </summary>
public sealed class HoldRemapShadow
{
    private HoldRemapEntry? _remap;
    private bool _holding;
    private bool _rolledOver;
    private bool _used;
    private long _downAt;
    private HeldButtons _owed;
    private KeyBits _claimed;
    private KeyBits _replayed;
    private KeyBits _ordered;
    private GroupId? _group;
    private KeyCode _ended;
    private int _pendingReplays;

    /// <summary>The hold remap held or followed (the plan entry claimed at its hold key's press); null otherwise.</summary>
    public HoldRemapEntry? Remap => _remap;

    /// <summary>A hold key is held (its press was claimed and its release has not come).</summary>
    public bool Holding => _holding;

    /// <summary>
    /// Holding, or following input buttons or keys pressed during a hold, or a rollover's replayed keys still down: the
    /// machine wants every button, wheel and key event. Otherwise it would pass them and change nothing.
    /// </summary>
    public bool Engaged => _holding || _owed != HeldButtons.None || !_claimed.IsEmpty || !_replayed.IsEmpty;

    /// <summary>Nothing engaged, no key replayed in order and no ended hold's key still down: a key event needs no look here unless it is a hold key's press or replays are in flight.</summary>
    public bool Idle => !Engaged && _ordered.IsEmpty && _ended == KeyCode.None;

    /// <summary>The app group whose plan the hold in progress (or last) was claimed with.</summary>
    public GroupId? Group => _group;

    /// <summary>Input buttons whose consumed down still owes a consumed up (A19).</summary>
    public HeldButtons OwedButtons => _owed;

    /// <summary>Messages whose injections (a tap, a replayed key) the worker has not made yet; while above zero, fresh key presses are replayed in order.</summary>
    public int PendingReplays => Volatile.Read(ref _pendingReplays);

    private bool Following => _owed != HeldButtons.None || !_claimed.IsEmpty;

    /// <summary>Everything this shadow decides from, to undo a decision whose event could not be enqueued (the replay count is not part of it).</summary>
    public Snapshot Save() => new(_remap, _holding, _rolledOver, _used, _downAt, _owed, _claimed, _replayed, _ordered, _group, _ended);

    public void Restore(Snapshot snapshot)
    {
        _remap = snapshot.Remap;
        _holding = snapshot.Holding;
        _rolledOver = snapshot.RolledOver;
        _used = snapshot.Used;
        _downAt = snapshot.DownAt;
        _owed = snapshot.Owed;
        _claimed = snapshot.Claimed;
        _replayed = snapshot.Replayed;
        _ordered = snapshot.Ordered;
        _group = snapshot.Group;
        _ended = snapshot.Ended;
    }

    /// <summary>
    /// True when a hold key is held with nothing owed (no input button, no input key) and the foreground's published plan is
    /// now another app group's, or none (<paramref name="published"/>): the hold ends (<see cref="EndByFocus"/>). A hold with
    /// inputs owed keeps following them, as the machine's <see cref="HoldRemapEvent.FocusMoved"/> rule says.
    /// </summary>
    public bool FocusMovedFrom(GroupId? published) => _holding && !Following && published != _group;

    /// <summary>Ends the hold as the machine does at <see cref="HoldRemapEvent.FocusMoved"/>: no tap, its key owned (swallowed) to its release.</summary>
    public void EndByFocus()
    {
        _ended = _remap!.HoldKey;
        _holding = false;
        _rolledOver = false;
        EndIfDone();
    }

    /// <summary>
    /// Forget the hold, every owed release and every claimed or replayed key. Only when the hook was reinstalled or the OS
    /// state is otherwise unknown (unlock, resume); the worker resets its machine at the same time. The replay count is left
    /// alone: the worker still counts down every message already queued.
    /// </summary>
    public void Reset() => Restore(default);

    /// <summary>Hook thread: a message the worker will inject for (and then call <see cref="ReplayDone"/>) is about to be posted.</summary>
    public void ReplayPosted() => Interlocked.Increment(ref _pendingReplays);

    /// <summary>The worker made the injections of one such message (or the hook could not post it).</summary>
    public void ReplayDone() => Interlocked.Decrement(ref _pendingReplays);

    /// <summary>
    /// A button's down or up, while <see cref="Engaged"/>: true when the hold remap takes it (suppressed, owed to its release),
    /// as the machine's button rows decide. A button pressed while holding counts as used, input or not (decision 4).
    /// </summary>
    public bool DecideButton(in RawInput input)
    {
        var flag = input.Button.Flag();
        if (input.Kind == RawInputKind.ButtonUp)
        {
            if ((_owed & flag) == HeldButtons.None)
            {
                return false;
            }

            _owed &= ~flag;
            EndIfDone();
            return true;
        }

        if ((_owed & flag) != HeldButtons.None)
        {
            // Its release was missed: still ours, still owed.
            return true;
        }

        _used |= _holding;
        var engaged = _holding ? !_rolledOver : _owed != HeldButtons.None;
        if (!engaged || !_remap!.IsInput(input.Button))
        {
            return false;
        }

        _owed |= flag;
        return true;
    }

    /// <summary>A wheel notch while <see cref="Holding"/>: it counts as used; true when it is an input of the hold remap (and not rolled over).</summary>
    public bool DecideWheel(in RawInput input)
    {
        if (!_holding || _rolledOver)
        {
            return false;
        }

        _used = true;
        return _remap!.IsInput(input.Wheel);
    }

    /// <summary>
    /// One key event. <paramref name="fresh"/> says a down starts a press the OS has not seen (else it is a repeat);
    /// <paramref name="candidate"/> is the foreground plan's hold remap on this key when the gate's conditions for claiming a
    /// hold key press hold (null otherwise), <paramref name="group"/> that plan's app group; <paramref name="capturing"/> is the
    /// hotkey capture flag, which takes fresh presses. The answer says what to post (if anything), the phase the machine sees,
    /// and whether the worker injects for it.
    /// </summary>
    public HoldKeyDecision DecideKey(KeyCode key, bool isUp, bool fresh, long timestampMs, HoldRemapEntry? candidate, bool capturing, GroupId? group = null)
    {
        var owned = isUp ? KeyPhase.Up : KeyPhase.Repeat;
        if (_holding && key == _remap!.HoldKey)
        {
            if (!isUp)
            {
                // A repeat, however long since the last event (key repeat may be off).
                return new(HoldKeyVerdict.Claimed, owned, false);
            }

            var tap = !_rolledOver && !_used && timestampMs - _downAt <= _remap.TapTimeMs;
            _holding = false;
            _rolledOver = false;
            EndIfDone();
            return new(HoldKeyVerdict.HoldUp, owned, tap);
        }

        if (key == _ended && _ended != KeyCode.None)
        {
            // The key of a hold focus ended: swallowed to its release, which the machine takes as that key's release.
            if (!isUp)
            {
                return new(HoldKeyVerdict.Claimed, owned, false);
            }

            _ended = KeyCode.None;
            return new(HoldKeyVerdict.HoldUp, owned, false);
        }

        if (_replayed.Has(key))
        {
            _replayed = isUp ? _replayed.Without(key) : _replayed;
            return new(HoldKeyVerdict.Claimed, owned, true);
        }

        if (_claimed.Has(key))
        {
            if (isUp)
            {
                _claimed = _claimed.Without(key);
                EndIfDone();
            }

            return new(HoldKeyVerdict.Claimed, owned, false);
        }

        if (_ordered.Has(key))
        {
            _ordered = isUp ? _ordered.Without(key) : _ordered;
            return new(HoldKeyVerdict.Ordered, owned, true);
        }

        var phase = isUp ? KeyPhase.Up : fresh ? KeyPhase.Down : KeyPhase.Repeat;
        var pressed = phase == KeyPhase.Down && !capturing;
        if (pressed && candidate is not null && !_holding && (!Following || candidate.Id == _remap!.Id))
        {
            // A new hold, or the same hold remap again while it follows buttons or keys still down (those count as used).
            _used = Following;
            _remap = candidate;
            _group = group;
            _holding = true;
            _rolledOver = false;
            _downAt = timestampMs;
            return new(HoldKeyVerdict.HoldDown, KeyPhase.Down, false);
        }

        var verdict = HoldKeyVerdict.None;
        if (Engaged && !(phase == KeyPhase.Down && capturing))
        {
            if (pressed && _holding && !_rolledOver)
            {
                var remap = _remap!;
                if (remap.IsInput(key))
                {
                    _used = true;
                    _claimed = _claimed.With(key);
                    return new(HoldKeyVerdict.Claimed, phase, false);
                }

                if (!HotkeyKeys.IsModifier(key) && !_used && timestampMs - _downAt <= remap.TapTimeMs)
                {
                    // Typing: the worker taps the hold key, then replays this key; off until the hold key is up.
                    _rolledOver = true;
                    _replayed = _replayed.With(key);
                    return new(HoldKeyVerdict.Claimed, phase, true);
                }
            }

            verdict = HoldKeyVerdict.Passed;
        }

        if (pressed && PendingReplays > 0)
        {
            _ordered = _ordered.With(key);
            return new(HoldKeyVerdict.Ordered, phase, true);
        }

        return new(verdict, phase, false);
    }

    private void EndIfDone()
    {
        if (!_holding && !Following)
        {
            _remap = null;
        }
    }

    /// <summary>What <see cref="Save"/> returns; opaque to callers.</summary>
    public readonly struct Snapshot
    {
        internal Snapshot(HoldRemapEntry? remap, bool holding, bool rolledOver, bool used, long downAt, HeldButtons owed, KeyBits claimed, KeyBits replayed, KeyBits ordered, GroupId? group, KeyCode ended)
        {
            Remap = remap;
            Holding = holding;
            RolledOver = rolledOver;
            Used = used;
            DownAt = downAt;
            Owed = owed;
            Claimed = claimed;
            Replayed = replayed;
            Ordered = ordered;
            Group = group;
            Ended = ended;
        }

        internal GroupId? Group { get; }

        internal KeyCode Ended { get; }

        internal HoldRemapEntry? Remap { get; }

        internal bool Holding { get; }

        internal bool RolledOver { get; }

        internal bool Used { get; }

        internal long DownAt { get; }

        internal HeldButtons Owed { get; }

        internal KeyBits Claimed { get; }

        internal KeyBits Replayed { get; }

        internal KeyBits Ordered { get; }
    }

    /// <summary>A set of keys as 256 bits, copied by value so a <see cref="Snapshot"/> allocates nothing; values outside 0..255 share bit 0.</summary>
    internal readonly record struct KeyBits(ulong B0, ulong B1, ulong B2, ulong B3)
    {
        public bool IsEmpty => (B0 | B1 | B2 | B3) == 0;

        public bool Has(KeyCode key)
        {
            var (word, bit) = Locate(key);
            return (Word(word) & bit) != 0;
        }

        public KeyBits With(KeyCode key)
        {
            var (word, bit) = Locate(key);
            return Set(word, Word(word) | bit);
        }

        public KeyBits Without(KeyCode key)
        {
            var (word, bit) = Locate(key);
            return Set(word, Word(word) & ~bit);
        }

        private static (int Word, ulong Bit) Locate(KeyCode key)
        {
            var value = (uint)key < 256 ? (int)key : 0;
            return (value >> 6, 1UL << (value & 63));
        }

        private ulong Word(int word) => word switch
        {
            0 => B0,
            1 => B1,
            2 => B2,
            _ => B3,
        };

        private KeyBits Set(int word, ulong value) => word switch
        {
            0 => this with { B0 = value },
            1 => this with { B1 = value },
            2 => this with { B2 = value },
            _ => this with { B3 = value },
        };
    }
}

/// <summary>What <see cref="HoldRemapShadow.DecideKey"/> wants done with a key event.</summary>
public enum HoldKeyVerdict
{
    /// <summary>Not the hold remap's business: nothing is posted for it; the key goes on to the other key paths.</summary>
    None,

    /// <summary>Forwarded to the machine, which lets it pass (it sees it to stay in step and changes nothing).</summary>
    Passed,

    /// <summary>The hold key's press, claimed: the machine starts (or resumes) a hold.</summary>
    HoldDown,

    /// <summary>The held hold key's release: swallowed; the machine ends the hold (and may tap).</summary>
    HoldUp,

    /// <summary>Forwarded to the machine and swallowed: the hold key's repeat, an input key's press, repeats and release, a rollover's key, a replayed key's repeats and release.</summary>
    Claimed,

    /// <summary>Swallowed only to keep the order while replays are in flight: the worker replays it itself; the machine never sees it.</summary>
    Ordered,
}

/// <summary>
/// The answer for one key event: what to post (<see cref="Verdict"/>), the phase the machine sees (a down of a key this
/// shadow owns is a repeat), and whether the worker injects for it in a way later keys must wait for (a tap, a replay).
/// </summary>
public readonly record struct HoldKeyDecision(HoldKeyVerdict Verdict, KeyPhase Phase, bool AwaitsInjection)
{
    /// <summary>True when the hold remap swallows the event.</summary>
    public bool Suppress => Verdict is not (HoldKeyVerdict.None or HoldKeyVerdict.Passed);
}
