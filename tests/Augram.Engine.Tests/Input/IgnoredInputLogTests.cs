using Augram.Core.Diagnostics;
using Augram.Engine.Input;
using SharpHook;
using SharpHook.Data;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>The hook's "ignored input" lines: one per slot per interval, slots kept apart, nothing while Info is off.</summary>
public sealed class IgnoredInputLogTests
{
    [Fact]
    public void AButtonSlotLogsOncePerInterval()
    {
        var clock = new FakeClock { MonotonicMs = 1_000 };
        var log = new ListEventLog();
        var ignored = new IgnoredInputLog(clock, log, ownCounted: true);

        ignored.Button(Button(MouseButton.Button4, EventType.MouseReleased), "simulated");
        clock.Advance(1_000);
        ignored.Button(Button(MouseButton.Button4, EventType.MouseReleased), "simulated");
        ignored.Button(Button(MouseButton.Button4, EventType.MousePressed), "simulated");
        clock.Advance((long)IgnoredInputLog.Interval.TotalMilliseconds);
        ignored.Button(Button(MouseButton.Button4, EventType.MouseReleased), "simulated");

        var lines = log.From(LogSources.Hook);
        Assert.Equal(["up", "down", "up"], lines.Select(line => line.Properties!.Single(p => p.Key == "direction").Value));
        Assert.All(lines, line => Assert.Equal("Button ignored", line.Message));
    }

    [Theory]
    [InlineData(true, "own scroll")]
    [InlineData(false, "simulated")]
    public void ASimulatedWheelEventsReasonSaysWhetherOwnNotchesAreCounted(bool ownCounted, string reason)
    {
        var log = new ListEventLog();
        var ignored = new IgnoredInputLog(new FakeClock(), log, ownCounted);

        ignored.Wheel(Wheel(MouseWheelScrollDirection.Vertical, rotation: -1));
        ignored.Wheel(Wheel(MouseWheelScrollDirection.Horizontal, rotation: -1));
        ignored.Wheel(Wheel(MouseWheelScrollDirection.Vertical, rotation: 0));
        ignored.Wheel(Wheel(MouseWheelScrollDirection.Vertical, rotation: 1));

        Assert.Equal([reason, "horizontal", "no rotation"], log.From(LogSources.Hook).Select(line => line.Properties!.Single(p => p.Key == "reason").Value));
    }

    [Fact]
    public void NothingIsLoggedWhileInfoIsOff()
    {
        var clock = new FakeClock { MonotonicMs = 5 };
        var quiet = new ListEventLog(EventLevel.Warning);
        var ignored = new IgnoredInputLog(clock, quiet, ownCounted: true);

        ignored.Button(Button(MouseButton.Button1, EventType.MousePressed), "unknown button");

        Assert.Empty(quiet.Events);
    }

    private static MouseHookEventArgs Button(MouseButton button, EventType type) => new(new UioHookEvent
    {
        Type = type,
        Mask = EventMask.SimulatedEvent,
        Mouse = new MouseEventData { Button = button, X = 1, Y = 2 },
    });

    private static MouseWheelHookEventArgs Wheel(MouseWheelScrollDirection direction, short rotation) => new(new UioHookEvent
    {
        Type = EventType.MouseWheel,
        Mask = EventMask.SimulatedEvent,
        Wheel = new MouseWheelEventData { Direction = direction, Rotation = rotation, X = 1, Y = 2 },
    });
}
