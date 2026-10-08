using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>Fake <see cref="IProcessLauncher"/>: records every request (thread-safe, the executor calls it) and answers <see cref="Result"/>; starts nothing.</summary>
internal sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly object _gate = new();
    private readonly List<ProcessLaunch> _launches = [];

    public ProcessLaunchResult Result { get; set; } = ProcessLaunchResult.Started;

    public IReadOnlyList<ProcessLaunch> Launches
    {
        get
        {
            lock (_gate)
            {
                return [.. _launches];
            }
        }
    }

    public ProcessLaunchResult Launch(ProcessLaunch launch)
    {
        lock (_gate)
        {
            _launches.Add(launch);
        }

        return Result;
    }
}
