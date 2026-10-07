using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>
/// Fake <see cref="IWindowOperations"/>: records every <see cref="Perform"/> call and answers with a
/// configurable support set, result and platform. Supports everything and succeeds by default.
/// Thread-safe because the executor calls it (the Core.Tests copy is single-threaded).
/// </summary>
internal sealed class FakeWindowOperations : IWindowOperations
{
    private readonly object _gate = new();
    private readonly List<(WindowOperation Operation, WindowIdentity Window, WindowSize? Size)> _calls = [];

    public HostPlatform Platform { get; set; } = HostPlatform.Windows;

    /// <summary>Operations <see cref="Supports"/> declines; empty means everything is supported.</summary>
    public HashSet<WindowOperation> Unsupported { get; } = [];

    public WindowOperationResult Result { get; set; } = WindowOperationResult.Ok;

    public IReadOnlyList<(WindowOperation Operation, WindowIdentity Window, WindowSize? Size)> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public bool Supports(WindowOperation operation) => !Unsupported.Contains(operation);

    public WindowOperationResult Perform(WindowOperation operation, WindowIdentity window, WindowSize? size = null)
    {
        lock (_gate)
        {
            _calls.Add((operation, window, size));
        }

        return Result;
    }
}
