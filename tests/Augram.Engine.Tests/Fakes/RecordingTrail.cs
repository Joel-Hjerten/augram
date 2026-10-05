using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Engine.Tests.Fakes;

/// <summary>Records the trail calls as <c>begin</c>, <c>extend</c>, <c>end</c> in order; thread-safe because the worker calls it.</summary>
internal sealed class RecordingTrail : IStrokeTrail
{
    private readonly object _gate = new();
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public int BeginCount => Calls.Count(call => call == "begin");

    public int EndCount => Calls.Count(call => call == "end");

    public void Begin(CapturePoint start) => Add("begin");

    public void Extend(CapturePoint point) => Add("extend");

    public void End() => Add("end");

    private void Add(string call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }
}
