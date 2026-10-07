using Augram.App.Components.HotkeyCapture;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>The App's key capture over a real <see cref="EngineHost"/> on fakes (no hook): marshals events, and answers null without a running engine.</summary>
public sealed class EngineKeyCaptureTests
{
    [Fact]
    public void ARunningEngineCapturesAndEveryEventIsMarshalled()
    {
        using var engine = new EngineFixture();
        var marshalled = 0;
        var seen = new List<KeyCaptureEvent>();
        var capture = new EngineKeyCapture(engine.Host, action =>
        {
            Interlocked.Increment(ref marshalled);
            lock (seen)
            {
                action();
            }
        });

        var handle = capture.Begin(seen.Add, TimeSpan.FromMinutes(1));

        Assert.NotNull(handle);
        Assert.True(engine.Host.IsCapturingKeys);
        Assert.True(engine.Source.Deliver(RawInput.KeyDown(KeyCode.L, 0, KeyModifiers.Meta)));
        EngineFixture.WaitFor(() => Volatile.Read(ref marshalled) == 1, "the marshalled key");
        handle.Dispose();

        Assert.False(engine.Host.IsCapturingKeys);
        lock (seen)
        {
            Assert.Equal([KeyCaptureEvent.KeyDown(KeyCode.L, KeyModifiers.Meta)], seen);
        }
    }

    [Fact]
    public void AnEngineThatIsNotRunningCannotCapture()
    {
        using var engine = new EngineFixture(start: false);
        var capture = new EngineKeyCapture(engine.Host, action => action());

        Assert.Null(capture.Begin(_ => { }, TimeSpan.FromSeconds(1)));
        Assert.False(engine.Host.IsCapturingKeys);
    }
}
