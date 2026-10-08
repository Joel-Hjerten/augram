using Augram.Core.Abstractions;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>Fake <see cref="IProcessLauncher"/>: records every request and answers with <see cref="Result"/>; starts nothing.</summary>
internal sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly List<ProcessLaunch> _launches = [];

    public ProcessLaunchResult Result { get; set; } = ProcessLaunchResult.Started;

    public IReadOnlyList<ProcessLaunch> Launches => _launches;

    public ProcessLaunchResult Launch(ProcessLaunch launch)
    {
        _launches.Add(launch);
        return Result;
    }
}
