using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>
/// Tells Augram's own injected button releases from other programs' (plan 0005 decision 10). Another program that swallows a
/// real release and posts its own (Eyeris's loupe chord) leaves the button up in the OS while Augram, which drops simulated
/// input, still counts it down. So the hook takes a simulated release as "released elsewhere", except the ones Augram posted:
/// <see cref="SharpHookInputSimulator"/> (and the macOS remap wrapper) announce each release with <see cref="Expect"/> before
/// posting it, and the hook claims one per simulated release of that button it sees (<see cref="TryClaim"/>). Without the claim,
/// a replayed click's release could end the next real press of the same button. Announcements that never come back (a failed
/// post, another program's hook swallowing ours) expire <see cref="Lifetime"/> after that button's last announcement, so a lost
/// one cannot hide a foreign release for long. Posts run on the worker or the command executor, the claim on the hook thread: a
/// lock, held for a few instructions.
/// </summary>
public sealed class OwnButtonInjections
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(1);

    private static readonly int ButtonCount = Enum.GetValues<MouseButton>().Length;

    private readonly Func<long> _nowMs;
    private readonly object _gate = new();
    private readonly int[] _pending = new int[ButtonCount];
    private readonly long[] _expiresAtMs = new long[ButtonCount];

    /// <param name="nowMs">A monotonic clock in milliseconds; the default is <see cref="Environment.TickCount64"/>.</param>
    public OwnButtonInjections(Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (static () => Environment.TickCount64);
    }

    /// <summary>Releases of <paramref name="button"/> announced and not yet seen by the hook (after expiry, zero).</summary>
    public int Pending(MouseButton button)
    {
        lock (_gate)
        {
            return _nowMs() > _expiresAtMs[(int)button] ? 0 : _pending[(int)button];
        }
    }

    /// <summary>Worker or executor: one release of <paramref name="button"/> of ours is about to be posted.</summary>
    public void Expect(MouseButton button)
    {
        lock (_gate)
        {
            var now = _nowMs();
            var i = (int)button;
            _pending[i] = now > _expiresAtMs[i] ? 1 : _pending[i] + 1;
            _expiresAtMs[i] = now + (long)Lifetime.TotalMilliseconds;
        }
    }

    /// <summary>Worker or executor: an announced release was not posted after all (the post failed).</summary>
    public void Withdraw(MouseButton button)
    {
        lock (_gate)
        {
            var i = (int)button;
            if (_pending[i] > 0)
            {
                _pending[i]--;
            }
        }
    }

    /// <summary>Hook thread, for a simulated release of <paramref name="button"/>: true when it is one of ours (and is now accounted for).</summary>
    public bool TryClaim(MouseButton button)
    {
        lock (_gate)
        {
            var i = (int)button;
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
