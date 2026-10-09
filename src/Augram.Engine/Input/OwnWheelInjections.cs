namespace Augram.Engine.Input;

/// <summary>
/// Tells Augram's own injected wheel notches from other programs' (2026-10-09). A vendor tool such as Logi Options+ re-posts
/// every wheel turn of its mouse, so all of them arrive with <c>IsEventSimulated</c> set; dropping those as the hook does for
/// buttons left wheel triggers nothing to see. So the hook takes simulated wheel events, except the ones the Scroll step
/// injected: <see cref="SharpHookInputSimulator.Scroll"/> announces each notch with <see cref="Expect"/> before posting it, and
/// the hook claims one per simulated vertical wheel event it sees (<see cref="TryClaim"/>). Without the claim, a Scroll step
/// fired by a wheel trigger would fire the trigger again, for as long as the stroke button is held. Announced notches that never
/// come back expire <see cref="Lifetime"/> after the last announcement, so a lost one cannot swallow a real turn for long.
/// Scroll runs on the command executor, the claim on the hook thread: a lock, held for a few instructions.
/// </summary>
public sealed class OwnWheelInjections
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(1);

    private readonly Func<long> _nowMs;
    private readonly object _gate = new();
    private int _pending;
    private long _expiresAtMs;

    /// <param name="nowMs">A monotonic clock in milliseconds; the default is <see cref="Environment.TickCount64"/>.</param>
    public OwnWheelInjections(Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (static () => Environment.TickCount64);
    }

    /// <summary>Notches announced and not yet seen by the hook (after expiry, zero).</summary>
    public int Pending
    {
        get
        {
            lock (_gate)
            {
                return _nowMs() > _expiresAtMs ? 0 : _pending;
            }
        }
    }

    /// <summary>Command executor: <paramref name="notches"/> wheel events of ours are about to be posted.</summary>
    public void Expect(int notches)
    {
        if (notches <= 0)
        {
            return;
        }

        lock (_gate)
        {
            var now = _nowMs();
            _pending = now > _expiresAtMs ? notches : _pending + notches;
            _expiresAtMs = now + (long)Lifetime.TotalMilliseconds;
        }
    }

    /// <summary>Hook thread, for a simulated vertical wheel event: true when it is one of ours (and is now accounted for).</summary>
    public bool TryClaim()
    {
        lock (_gate)
        {
            if (_pending == 0 || _nowMs() > _expiresAtMs)
            {
                _pending = 0;
                return false;
            }

            _pending--;
            return true;
        }
    }
}
