using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Recognition;
using Augram.Engine.Hosting;

namespace Augram.App.Tests.Support;

/// <summary>A settings store and an <see cref="EngineHost"/> over fakes (no hook), for the hosting services that bridge the two.</summary>
internal sealed class EngineFixture : IDisposable
{
    // Generous: only a failing test waits this long. Two Windows CI runs missed 5 s (2026-10-07) while other tests spun.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    public EngineFixture(bool start = true, Settings? settings = null)
    {
        Settings = new SettingsStore(settings ?? Core.Config.Settings.Default);
        Host = new EngineHost(
            new EnginePorts { Input = Source, Simulator = Simulator, Log = Log },
            () => [],
            () => RecognitionOptions.Default,
            new EngineHostOptions(Settings.Current.General.StrokeButton, HealthPollInterval: TimeSpan.FromHours(1)));
        if (start)
        {
            Host.Start();
        }
    }

    public SettingsStore Settings { get; }

    public FakeInputSource Source { get; } = new();

    public FakeInputSimulator Simulator { get; } = new();

    public ListEventLog Log { get; } = new();

    public EngineHost Host { get; }

    /// <summary>
    /// Polls <paramref name="condition"/> every 10 ms instead of spinning: the conditions wait on the engine worker or a
    /// thread-pool timer, and busy-spinning tests running in parallel starved exactly those threads on a small CI runner.
    /// </summary>
    public static void WaitFor(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"timed out waiting for {what}");
            }

            Thread.Sleep(PollInterval);
        }
    }

    public void Press(Core.Capture.MouseButton button, long t = 0)
    {
        Source.Deliver(RawInput.ButtonDown(button, 10, 10, t));
        Source.Deliver(RawInput.ButtonUp(button, 10, 10, t + 10));
    }

    public void Dispose() => Host.Dispose();
}
