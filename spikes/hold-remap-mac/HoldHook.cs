using System.Diagnostics;
using SharpHook;
using SharpHook.Data;

namespace HoldRemapSpike;

/// <summary>
/// The hook thread's handlers: decide suppress or pass, record, hand the decision to the worker, return. No posting,
/// no console output, no lookups (invariant 1). Simulated events (our own posts, and any other process's) are dropped
/// before anything else. Pairing: nothing is suppressed unless Space was claimed or a button is owed, and every
/// suppressed press gets its release suppressed, also after Space is up and while stopping.
/// </summary>
internal sealed class HoldHook
{
    private readonly Worker _worker;
    private readonly bool _anywhere;
    private readonly bool _variantB;

    // Hook thread only.
    private bool _active;
    private bool _used;
    private bool _spaceDownSeenByOs;
    private long _pressedAt;
    private int _owed;

    private volatile bool _blenderFront;
    private volatile bool _stopping;
    private Exception? _fault;

    public HoldHook(Options options, Worker worker)
    {
        _worker = worker;
        _anywhere = options.Anywhere;
        _variantB = options.VariantB;
    }

    /// <summary>Written by the main thread every 100 ms.</summary>
    public bool BlenderFront
    {
        get => _blenderFront;
        set => _blenderFront = value;
    }

    /// <summary>Set before the worker releases: nothing new is claimed, owed releases are still swallowed.</summary>
    public bool Stopping
    {
        get => _stopping;
        set => _stopping = value;
    }

    public Exception? Fault => Volatile.Read(ref _fault);

    public void OnKey(object? sender, KeyboardHookEventArgs e)
    {
        try
        {
            e.SuppressEvent = !e.IsEventSimulated && e.Data.KeyCode == KeyCode.VcSpace && Space(e.RawEvent.Type == EventType.KeyPressed);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public void OnButton(object? sender, MouseHookEventArgs e)
    {
        try
        {
            e.SuppressEvent = !e.IsEventSimulated && Button(e);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public void OnMove(object? sender, MouseHookEventArgs e)
    {
        try
        {
            // Variant B only: while an output is held, swallow the physical move/drag; the worker re-posts it as a Middle drag.
            if (e.IsEventSimulated || !_variantB || _stopping || Remap.For(_owed) == Output.None)
            {
                return;
            }

            e.SuppressEvent = true;
            _worker.Post(new Msg(MsgKind.Drag, _owed, e.Data.X, e.Data.Y, Stopwatch.GetTimestamp()));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public void OnWheel(object? sender, MouseWheelHookEventArgs e)
    {
        try
        {
            // A wheel turn while Space is held cancels the tap (plan 0002 decision 4); it is never suppressed here.
            if (e.IsEventSimulated || !_active || _used || _stopping)
            {
                return;
            }

            _used = true;
            _worker.Post(new Msg(MsgKind.Wheel, _owed, e.Data.X, e.Data.Y, Stopwatch.GetTimestamp()));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private bool Space(bool pressed)
    {
        if (pressed)
        {
            if (_active)
            {
                return true; // a repeat of our claimed press
            }

            if (_spaceDownSeenByOs)
            {
                return false; // a repeat of a press the OS saw: its release must reach the OS too
            }

            if (_stopping || !(_anywhere || _blenderFront))
            {
                _spaceDownSeenByOs = true;
                return false;
            }

            _active = true;
            _used = false;
            _pressedAt = Stopwatch.GetTimestamp();
            _worker.Post(new Msg(MsgKind.HoldDown, _owed, 0, 0, _pressedAt));
            return true;
        }

        if (!_active)
        {
            _spaceDownSeenByOs = false;
            return false;
        }

        _active = false;
        var now = Stopwatch.GetTimestamp();
        _worker.Post(new Msg(MsgKind.HoldUp, _owed, 0, 0, now, now - _pressedAt, _used));
        return true;
    }

    private bool Button(MouseHookEventArgs e)
    {
        var bit = e.Data.Button switch
        {
            MouseButton.Button1 => Remap.Left,
            MouseButton.Button2 => Remap.Right,
            MouseButton.Button3 => Remap.Middle,
            _ => 0,
        };

        if (e.RawEvent.Type == EventType.MousePressed)
        {
            if (!_active || _stopping)
            {
                return false;
            }

            _used = true;
            if (bit == 0)
            {
                _worker.Post(new Msg(MsgKind.OtherButton, (int)e.Data.Button, e.Data.X, e.Data.Y, Stopwatch.GetTimestamp()));
                return false; // any other button passes, but cancels the tap
            }

            if ((_owed & bit) == 0)
            {
                _owed |= bit;
                _worker.Post(new Msg(MsgKind.Buttons, _owed, e.Data.X, e.Data.Y, Stopwatch.GetTimestamp()));
            }

            return true;
        }

        if (bit == 0 || (_owed & bit) == 0)
        {
            return false;
        }

        // A swallowed press always gets its release swallowed, whether or not Space is still held.
        _owed &= ~bit;
        _worker.Post(new Msg(MsgKind.Buttons, _owed, e.Data.X, e.Data.Y, Stopwatch.GetTimestamp()));
        return true;
    }

    private void Fail(Exception ex)
    {
        Interlocked.CompareExchange(ref _fault, ex, null);
        Program.RequestStop("hook handler error");
    }
}
