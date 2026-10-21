using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>
/// Tells Augram's own injected input from other programs', one ledger per <see cref="InjectionSlot"/>: the wheel, each mouse
/// button's releases and presses, and moves. The hook took simulated input in two steps, each needing this ledger, and since
/// 0.20.5 takes every simulated mouse event it does not claim, on both platforms (<see cref="ForeignInput.Take"/>, third item):
/// <list type="bullet">
/// <item><b>Wheel</b> (2026-10-09). A vendor tool such as Logi Options+ re-posts every wheel turn of its mouse, so all of them
/// arrive with <c>IsEventSimulated</c> set; dropping those left wheel triggers nothing to see. So the hook takes simulated
/// vertical wheel events, except the Scroll step's own: <see cref="SharpHookInputSimulator.Scroll"/> announces each notch
/// before posting it, and the hook claims one per simulated vertical wheel event it sees. Without the claim, a Scroll step
/// fired by a wheel trigger would fire the trigger again, for as long as the stroke button is held.</item>
/// <item><b>Button releases</b> (plan 0005 decision 10). Another program that swallows a real release and posts its own
/// (Eyeris's loupe chord) leaves the button up in the OS while Augram, which drops simulated input, still counts it down. So the
/// hook takes a simulated release as "released elsewhere", except Augram's own: <see cref="SharpHookInputSimulator"/> (and the
/// macOS remap wrapper, through an <c>Action&lt;MouseButton&gt;</c> since Platform may not reference the Engine) announce each
/// release before posting it, and <see cref="Withdraw"/> it when the post fails; the hook claims one per simulated release of
/// that button. Without the claim, a replayed click's release could end the next real press of the same button.</item>
/// <item><b>Presses and moves</b> (<see cref="ForeignInput.Take"/>: macOS 2026-10-19, Windows 2026-10-21). Both hooks mark
/// input any other process posts as simulated, the Wacom driver's pen among them (on Windows with Windows Ink on), so an unclaimed
/// simulated press, release or move is taken as real. The two simulators announce every press (<see cref="PressSlotOf"/>) and
/// every move or drag (<see cref="InjectionSlot.Move"/>) they post, on Windows also the move libuiohook posts before a press or
/// release at a point, so Augram's own come back claimed and dropped; without the move claim a hold remap's re-posted drag would
/// be swallowed and re-posted again without end.</item>
/// </list>
/// Announcements that never come back (a failed post, another program's hook swallowing ours) expire <see cref="Lifetime"/>
/// after that slot's last announcement, so a lost one cannot hide foreign input for long. Announcements come from the worker or
/// the command executor, the claim from the hook thread: one lock, held for a few instructions. One instance, shared by the
/// source and the simulator (<c>App/Hosting/EngineModule</c>).
/// </summary>
public sealed class OwnInjections
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(1);

    private static readonly int SlotCount = Enum.GetValues<InjectionSlot>().Length;

    private readonly Func<long> _nowMs;
    private readonly object _gate = new();
    private readonly int[] _pending = new int[SlotCount];
    private readonly long[] _expiresAtMs = new long[SlotCount];

    /// <param name="nowMs">A monotonic clock in milliseconds; the default is <see cref="Environment.TickCount64"/>.</param>
    public OwnInjections(Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (static () => Environment.TickCount64);
    }

    /// <summary>The slot of <paramref name="button"/>'s releases: the same number, so the hook's lookup is a cast.</summary>
    public static InjectionSlot ReleaseSlotOf(MouseButton button) => (InjectionSlot)button;

    /// <summary>The slot of <paramref name="button"/>'s presses: <see cref="InjectionSlot.LeftPress"/> onwards, in button order.</summary>
    public static InjectionSlot PressSlotOf(MouseButton button) => InjectionSlot.LeftPress + ((int)button - (int)MouseButton.Left);

    /// <summary>Injections into <paramref name="slot"/> announced and not yet seen by the hook (after expiry, zero).</summary>
    public int Pending(InjectionSlot slot)
    {
        lock (_gate)
        {
            return _nowMs() > _expiresAtMs[(int)slot] ? 0 : _pending[(int)slot];
        }
    }

    /// <summary>Worker or executor: <paramref name="count"/> injections into <paramref name="slot"/> of ours are about to be posted; none for zero or less.</summary>
    public void Expect(InjectionSlot slot, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_gate)
        {
            var now = _nowMs();
            var i = (int)slot;
            _pending[i] = now > _expiresAtMs[i] ? count : _pending[i] + count;
            _expiresAtMs[i] = now + (long)Lifetime.TotalMilliseconds;
        }
    }

    /// <summary>Worker or executor: one announced injection was not posted after all (the post failed).</summary>
    public void Withdraw(InjectionSlot slot)
    {
        lock (_gate)
        {
            var i = (int)slot;
            if (_pending[i] > 0)
            {
                _pending[i]--;
            }
        }
    }

    /// <summary>Hook thread, for a simulated event of <paramref name="slot"/>: true when it is one of ours (and is now accounted for).</summary>
    public bool TryClaim(InjectionSlot slot)
    {
        lock (_gate)
        {
            var i = (int)slot;
            if (_pending[i] == 0 || _nowMs() > _expiresAtMs[i])
            {
                _pending[i] = 0;
                return false;
            }

            _pending[i]--;
            return true;
        }
    }
}

