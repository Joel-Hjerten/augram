using System.Collections.Concurrent;
using System.Diagnostics;

namespace HoldRemapSpike;

internal enum MsgKind
{
    HoldDown,
    HoldUp,
    Buttons,
    OtherButton,
    Wheel,
    Drag,
    Stop,
}

/// <summary>One hook decision. <see cref="Timestamp"/> is <see cref="Stopwatch"/> ticks taken in the hook.</summary>
internal readonly record struct Msg(MsgKind Kind, int Buttons, int X, int Y, long Timestamp, long HeldTicks = 0, bool Used = false);

internal enum Output
{
    None,
    Middle,
    ShiftMiddle,
    CtrlMiddle,
}

/// <summary>Blender's inputs, hard-coded: the held button set (exact match) to the output held for it.</summary>
internal static class Remap
{
    public const int Left = 1;
    public const int Right = 2;
    public const int Middle = 4;

    public static Output For(int buttons) => buttons switch
    {
        Left => Output.Middle,
        Right => Output.ShiftMiddle,
        Middle => Output.CtrlMiddle,
        Left | Right => Output.CtrlMiddle,
        _ => Output.None,
    };

    public static Modifier ModifierOf(Output output) => output switch
    {
        Output.ShiftMiddle => Modifier.Shift,
        Output.CtrlMiddle => Modifier.Control,
        _ => Modifier.None,
    };

    public static string Describe(Output output) => output switch
    {
        Output.Middle => "Middle",
        Output.ShiftMiddle => "Shift+Middle",
        Output.CtrlMiddle => "Ctrl+Middle",
        _ => "none",
    };

    public static string Describe(int buttons)
    {
        var names = new List<string>(3);
        if ((buttons & Left) != 0)
        {
            names.Add("Left");
        }

        if ((buttons & Right) != 0)
        {
            names.Add("Right");
        }

        if ((buttons & Middle) != 0)
        {
            names.Add("Middle");
        }

        return "{" + string.Join(",", names) + "}";
    }
}

/// <summary>
/// The worker thread: takes the hook's decisions in order, does all posting and all logging. The output state is
/// guarded by <see cref="_gate"/> so <see cref="EmergencyRelease"/> can release from another thread if this one is stuck.
/// </summary>
internal sealed class Worker
{
    private readonly BlockingCollection<Msg> _queue = [];
    private readonly ManualResetEventSlim _released = new();
    private readonly object _gate = new();
    private readonly Options _options;
    private readonly IPoster _poster;
    private Output _current;
    private Modifier _modifierDown;
    private bool _stopped;
    private int _lastX;
    private int _lastY;
    private int _dragCount;
    private int _dragFailures;
    private double _dragSumMs;
    private double _dragWorstMs;
    private long _dragsSince;

    public Worker(Options options, IPoster poster)
    {
        _options = options;
        _poster = poster;
    }

    public string PosterDescription => _poster.Describe;

    public void Start() => new Thread(Run) { IsBackground = true, Name = "spike-worker" }.Start();

    /// <summary>Hook thread: unbounded, never blocks.</summary>
    public void Post(in Msg msg) => _queue.TryAdd(msg);

    /// <summary>Releases whatever output is held and stops posting anything new; true once the worker has done it.</summary>
    public bool StopAndRelease(TimeSpan timeout)
    {
        Post(new Msg(MsgKind.Stop, 0, 0, 0, Stopwatch.GetTimestamp()));
        return _released.Wait(timeout);
    }

    /// <summary>For a worker that did not answer <see cref="StopAndRelease"/>: releases from the calling thread.</summary>
    public void EmergencyRelease()
    {
        if (!Monitor.TryEnter(_gate, TimeSpan.FromSeconds(1)))
        {
            Program.Log("emergency release: the worker holds its lock; Middle may still be down, press and release Middle once");
            return;
        }

        try
        {
            _stopped = true;
            ReleaseAll("emergency release");
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private void Run()
    {
        while (true)
        {
            if (_queue.TryTake(out var msg, 250))
            {
                try
                {
                    lock (_gate)
                    {
                        Handle(msg);
                    }
                }
                catch (Exception e)
                {
                    Program.Log($"worker error: {e}");
                    Program.RequestStop("worker error");
                }

                if (msg.Kind == MsgKind.Stop)
                {
                    _released.Set();
                }
            }

            lock (_gate)
            {
                FlushDragStats(force: false);
            }
        }
    }

    private void Handle(in Msg msg)
    {
        switch (msg.Kind)
        {
            case MsgKind.HoldDown:
                Program.Log(msg.Buttons == 0 ? "hold Space" : $"hold Space (still owed: {Remap.Describe(msg.Buttons)})");
                break;
            case MsgKind.HoldUp:
                OnHoldUp(msg);
                break;
            case MsgKind.Buttons:
                OnButtons(msg);
                break;
            case MsgKind.OtherButton:
                Program.Log($"button {msg.Buttons} passed through; no tap at release");
                break;
            case MsgKind.Wheel:
                Program.Log("wheel passed through; no tap at release");
                break;
            case MsgKind.Drag:
                OnDrag(msg);
                break;
            case MsgKind.Stop:
                FlushDragStats(force: true);
                _stopped = true;
                ReleaseAll("stop");
                break;
        }
    }

    private void OnHoldUp(in Msg msg)
    {
        var held = Ms(msg.HeldTicks);
        if (_stopped)
        {
            Program.Log($"hold up {held:0} ms → no tap: stopping");
        }
        else if (msg.Used)
        {
            Program.Log($"hold up {held:0} ms → no tap: used");
        }
        else if (held > _options.TapMs)
        {
            Program.Log($"hold up {held:0} ms → no tap: held longer than {_options.TapMs} ms");
        }
        else
        {
            var ok = _poster.SpaceTap();
            Program.Log($"hold up {held:0} ms → tap sent{(ok ? "" : " (a post FAILED)")}");
        }
    }

    /// <summary>The held set changed: release the current output first, then press the new one, modifiers around its down only.</summary>
    private void OnButtons(in Msg msg)
    {
        FlushDragStats(force: true);
        _lastX = msg.X;
        _lastY = msg.Y;
        var desired = _stopped ? Output.None : Remap.For(msg.Buttons);
        var set = Remap.Describe(msg.Buttons);
        if (desired == _current)
        {
            Program.Log($"set {set} → {Remap.Describe(desired)} (unchanged)");
            return;
        }

        var ok = true;
        if (_current != Output.None)
        {
            ok &= _poster.MiddleUp();
            _current = Output.None;
        }

        if (desired != Output.None)
        {
            var modifier = Remap.ModifierOf(desired);
            if (modifier != Modifier.None)
            {
                ok &= _poster.ModifierDown(modifier);
                _modifierDown = modifier;
            }

            ok &= _poster.MiddleDown(msg.X, msg.Y, modifier);
            _current = desired;
            if (modifier != Modifier.None)
            {
                ok &= _poster.ModifierUp(modifier);
                _modifierDown = Modifier.None;
            }
        }

        var switchMs = Ms(Stopwatch.GetTimestamp() - msg.Timestamp);
        Program.Log($"set {set} → {Remap.Describe(desired)} (switch {switchMs:0.0} ms){(ok ? "" : ", a post FAILED")}");
    }

    private void OnDrag(in Msg msg)
    {
        var dx = msg.X - _lastX;
        var dy = msg.Y - _lastY;
        _lastX = msg.X;
        _lastY = msg.Y;
        if (_current == Output.None || _stopped)
        {
            return;
        }

        var ok = NativePoster.Dragged(msg.X, msg.Y, dx, dy);
        var now = Stopwatch.GetTimestamp();
        var lag = Ms(now - msg.Timestamp);
        if (_dragCount == 0)
        {
            _dragsSince = now;
        }

        _dragCount++;
        _dragSumMs += lag;
        _dragWorstMs = Math.Max(_dragWorstMs, lag);
        if (!ok)
        {
            _dragFailures++;
        }
    }

    private void FlushDragStats(bool force)
    {
        if (_dragCount == 0)
        {
            return;
        }

        var span = Stopwatch.GetElapsedTime(_dragsSince);
        if (!force && span < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var failed = _dragFailures == 0 ? "" : $", {_dragFailures} FAILED";
        Program.Log($"drags re-posted: {_dragCount} in {span.TotalSeconds:0.0} s, hook→posted avg {_dragSumMs / _dragCount:0.000} ms, worst {_dragWorstMs:0.000} ms{failed}");
        _dragCount = 0;
        _dragFailures = 0;
        _dragSumMs = 0;
        _dragWorstMs = 0;
    }

    private void ReleaseAll(string why)
    {
        var parts = new List<string>(2);
        if (_current != Output.None)
        {
            _poster.MiddleUp();
            parts.Add($"Middle up (was {Remap.Describe(_current)})");
            _current = Output.None;
        }

        if (_modifierDown != Modifier.None)
        {
            _poster.ModifierUp(_modifierDown);
            parts.Add($"{_modifierDown} up");
            _modifierDown = Modifier.None;
        }

        Program.Log($"{why}: {(parts.Count == 0 ? "nothing was held" : string.Join(", ", parts))}");
    }
}
