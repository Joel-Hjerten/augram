namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// The cheap window key under a point (<c>IWindowSystem.WindowKeyAt</c>, the <c>CGWindowID</c>) for the ignore list's watch.
/// macOS has no call that names the window under a point without the window server's whole list, so this hit-tests a copy
/// of the list that is at most <see cref="MaxAge"/> old: a pointer moving inside one window costs a walk over the copy, not a
/// window-server round trip per move. <see cref="Invalidate"/> drops the copy (the frontmost app changed, so the list
/// probably did). The reader and the clock are injected so the policy is tested on every OS; <c>MacWindowSystem</c> passes
/// the real ones. A window that opened or moved within the last <see cref="MaxAge"/> may be missed until the copy is
/// refreshed; the watch then asks again on the next pointer move.
/// </summary>
internal sealed class MacWindowKeys
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMilliseconds(250);

    private readonly Func<(IReadOnlyList<MacWindowInfo> Windows, IReadOnlyList<MacRect> Displays)> _read;
    private readonly Func<long> _nowMs;
    private readonly int _ownProcessId;
    private readonly object _gate = new();
    private IReadOnlyList<MacWindowInfo> _windows = [];
    private IReadOnlyList<MacRect> _displays = [];
    private long _readAtMs;
    private bool _valid;

    public MacWindowKeys(Func<(IReadOnlyList<MacWindowInfo> Windows, IReadOnlyList<MacRect> Displays)> read, Func<long> nowMs, int ownProcessId)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(nowMs);
        _read = read;
        _nowMs = nowMs;
        _ownProcessId = ownProcessId;
    }

    /// <summary>How many times the list was read: what the tests count.</summary>
    public int Reads { get; private set; }

    /// <summary>The <c>CGWindowID</c> of the window <see cref="MacWindowPick.At"/> picks at the point, or 0 for none.</summary>
    public uint KeyAt(double x, double y)
    {
        lock (_gate)
        {
            var now = _nowMs();
            if (!_valid || now - _readAtMs >= (long)MaxAge.TotalMilliseconds)
            {
                (_windows, _displays) = _read();
                _readAtMs = now;
                _valid = true;
                Reads++;
            }

            return MacWindowPick.At(_windows, x, y, _ownProcessId, _displays)?.Id ?? 0;
        }
    }

    /// <summary>The next <see cref="KeyAt"/> reads the list again.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _valid = false;
        }
    }
}
