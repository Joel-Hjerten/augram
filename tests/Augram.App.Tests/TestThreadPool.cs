using System.Runtime.CompilerServices;

namespace Augram.App.Tests;

/// <summary>
/// Gives the thread pool enough threads from the start for this assembly's tests. Many tests wait on a timer or a worker
/// while others block, and on a small CI runner the pool adds threads slowly: a zero-delay timer once took over 5 s to
/// fire on Windows (2026-10-07). With a higher minimum, blocked test threads cannot starve the callbacks they wait for.
/// </summary>
internal static class TestThreadPool
{
    private const int MinimumWorkers = 32;

    [ModuleInitializer]
    internal static void Raise()
    {
        ThreadPool.GetMinThreads(out var workers, out var completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers, MinimumWorkers), completionPorts);
    }
}
