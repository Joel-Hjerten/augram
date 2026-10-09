using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.Engine.Hosting;

/// <summary>
/// One pass of the <see cref="IgnoreListWatch"/>, on its thread only: which window is under the pointer and what has focus,
/// looked up as little as possible, what the ignore list says about them (<see cref="IgnoreList"/>), and which buttons are
/// anchors over that window (<see cref="AnchorPlanner"/>; Joel 2026-10-09: per app, cached per app group). Lookups are
/// deduplicated by window: the cheap keys of <see cref="IWindowSystem.WindowKeyAt"/> and <see cref="IWindowSystem.ForegroundKey"/>
/// are compared with the last ones, and <see cref="IWindowSystem.WindowAt"/> / <see cref="IWindowSystem.Foreground"/> run
/// only when a key changed (on a platform without cheap keys, at most every <c>slowLookupInterval</c>, the latest position
/// retried at <see cref="RetryAtMs"/>). A change of focus re-checks the pointer even when it did not move, since a window
/// brought to the front under a motionless pointer usually takes focus. Nothing is looked up while no active ignored app
/// can match on this platform and no active command holds a button besides the stroke button; the foreground identity is
/// read only while a "disable while focused" app is active.
/// </summary>
internal sealed class IgnoreLookup
{
    /// <summary>The packed position before the hook has seen the pointer.</summary>
    public const long NoPointer = long.MinValue;

    private readonly IWindowSystem _windows;
    private readonly HostPlatform _platform;
    private readonly IClock _clock;
    private readonly long _slowLookupMs;
    private MappingDocument? _seen;
    private bool _pausing;
    private bool _ignoring;
    private bool _anchoring;
    private MouseButton _planStrokeButton;
    private WindowIdentity? _planWindow;
    private bool _planStale = true;
    private readonly Dictionary<GroupId, AnchorPlan> _plans = [];
    private long _pointerSeen = NoPointer;
    private nint? _pointerKey;
    private WindowIdentity? _pointerWindow;
    private nint? _focusKey;
    private WindowIdentity? _focusWindow;
    private long _nextSlowLookupMs;

    public IgnoreLookup(IWindowSystem windows, HostPlatform platform, IClock clock, TimeSpan slowLookupInterval)
    {
        _windows = windows;
        _platform = platform;
        _clock = clock;
        _slowLookupMs = (long)slowLookupInterval.TotalMilliseconds;
    }

    /// <summary>An active ignored app can match here, or a command holds a button besides the stroke button (per-app anchors): the pointer and the focus are watched.</summary>
    public bool WatchesPointer { get; private set; }

    /// <summary>Some active command holds a button besides the stroke button: <see cref="Plan"/> follows the window under the pointer.</summary>
    public bool WatchesAnchors => _anchoring;

    /// <summary>The anchor plan over the window under the pointer as of the last pass (<see cref="AnchorPlanner"/>); <see cref="AnchorPlan.None"/> while no command holds another button.</summary>
    public AnchorPlan Plan { get; private set; }

    /// <summary>An active "disable while focused" app can match here: the foreground's identity is read when focus moves.</summary>
    public bool WatchesFocus => _pausing;

    /// <summary>The active ignored app (either mode) the window under the pointer belongs to, as of the last pass.</summary>
    public IgnoredApp? Over { get; private set; }

    /// <summary>The active "disable while focused" app that has focus, as of the last pass.</summary>
    public IgnoredApp? PausedBy { get; private set; }

    /// <summary>A pointer lookup was put off (no cheap key, too soon after the last): when to pass again, on the clock's milliseconds.</summary>
    public long? RetryAtMs { get; private set; }

    public static long Pack(int x, int y) => ((long)x << 32) | (uint)y;

    public static (int X, int Y) Unpack(long packed) => ((int)(packed >> 32), (int)packed);

    /// <summary>The last pass asked what has focus (it was due, or the mapping changed, or keys were dropped).</summary>
    public bool FocusChecked { get; private set; }

    /// <param name="mapping">The current snapshot; a new one re-reads what is watched and re-checks the focus.</param>
    /// <param name="pointer">The latest position the hook saw (<see cref="Pack"/>), or <see cref="NoPointer"/>.</param>
    /// <param name="forget">Drop every key (resume, unlock, display change): the next lookups start fresh.</param>
    /// <param name="checkFocus">Ask what has focus this pass: the watch says so at its polling pace, so a busy pointer does not ask on every pass.</param>
    /// <param name="strokeButton">The stroke button the anchor plans are worked out for (a plan names it as the stroke anchor).</param>
    public void Pass(MappingDocument mapping, long pointer, bool forget, bool checkFocus = true, MouseButton strokeButton = MouseButton.Right)
    {
        if (!ReferenceEquals(mapping, _seen))
        {
            _seen = mapping;
            _ignoring = IgnoreList.WatchesPointer(mapping, _platform);
            _anchoring = AnchorPlanner.UsesButtons(mapping, _platform);
            WatchesPointer = _ignoring || _anchoring;
            _pausing = IgnoreList.WatchesFocus(mapping, _platform);
            _focusKey = null;
            _planStale = true;
            checkFocus = true;
        }

        if (strokeButton != _planStrokeButton)
        {
            _planStrokeButton = strokeButton;
            _planStale = true;
        }

        if (forget || !WatchesPointer)
        {
            _pointerSeen = NoPointer;
            _pointerKey = null;
            _pointerWindow = null;
            _focusKey = null;
            _focusWindow = null;
            RetryAtMs = null;
            checkFocus = true;
        }

        FocusChecked = false;
        if (!WatchesPointer)
        {
            Over = null;
            PausedBy = null;
            Plan = AnchorPlan.None;
            return;
        }

        FocusChecked = checkFocus;
        var focusMoved = checkFocus && CheckFocus();
        CheckPointer(pointer, focusMoved);
        Over = _ignoring ? IgnoreList.Under(mapping, _pointerWindow, _platform) : null;
        PausedBy = _ignoring ? IgnoreList.PausedBy(mapping, _focusWindow, _platform) : null;
        Plan = _anchoring ? PlanFor(mapping, _pointerWindow) : AnchorPlan.None;
    }

    /// <summary>The plan over the window, worked out again only when the window, the mapping or the stroke button changed; cached per app group.</summary>
    private AnchorPlan PlanFor(MappingDocument mapping, WindowIdentity? window)
    {
        if (_planStale)
        {
            _plans.Clear();
            _planStale = false;
        }
        else if (ReferenceEquals(window, _planWindow))
        {
            return Plan;
        }

        _planWindow = window;
        var group = CommandResolver.FindGroup(mapping, window, _platform);
        var key = group?.Id ?? GroupId.Global;
        if (!_plans.TryGetValue(key, out var plan))
        {
            plan = AnchorPlanner.ForGroup(mapping, group, _platform, _planStrokeButton);
            _plans[key] = plan;
        }

        return plan;
    }

    /// <summary>True when what has focus changed since the last pass.</summary>
    private bool CheckFocus()
    {
        if (_windows.ForegroundKey() is { } key)
        {
            if (key == _focusKey)
            {
                return false;
            }

            _focusKey = key;
            _focusWindow = key != 0 && _pausing ? _windows.Foreground() : null;
            return true;
        }

        // No cheap key on this platform: the foreground itself, at the watch's polling pace, and only when it matters.
        _focusKey = null;
        var window = _pausing ? _windows.Foreground() : null;
        var moved = window?.RootHandle != _focusWindow?.RootHandle;
        _focusWindow = window;
        return moved;
    }

    private void CheckPointer(long pointer, bool force)
    {
        if (pointer == NoPointer || (pointer == _pointerSeen && !force && RetryAtMs is null))
        {
            return;
        }

        var (x, y) = Unpack(pointer);
        if (_windows.WindowKeyAt(x, y) is { } key)
        {
            RetryAtMs = null;
            _pointerSeen = pointer;
            if (key != _pointerKey)
            {
                _pointerKey = key;
                _pointerWindow = key == 0 ? null : _windows.WindowAt(x, y);
            }

            return;
        }

        var now = _clock.MonotonicMs;
        if (now < _nextSlowLookupMs)
        {
            RetryAtMs = _nextSlowLookupMs;
            return;
        }

        RetryAtMs = null;
        _pointerSeen = pointer;
        _pointerKey = null;
        _pointerWindow = _windows.WindowAt(x, y);
        _nextSlowLookupMs = now + _slowLookupMs;
    }
}
