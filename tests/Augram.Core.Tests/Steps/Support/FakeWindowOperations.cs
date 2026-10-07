using Augram.Core.Abstractions;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>
/// Fake <see cref="IWindowOperations"/>: records every <see cref="Perform"/> call and answers with a
/// configurable support set, result and platform. Supports everything and succeeds by default.
/// </summary>
internal sealed class FakeWindowOperations : IWindowOperations
{
    private readonly List<(WindowOperation Operation, WindowIdentity Window, WindowSize? Size)> _calls = [];

    public HostPlatform Platform { get; set; } = HostPlatform.Windows;

    /// <summary>Operations <see cref="Supports"/> declines; empty means everything is supported.</summary>
    public HashSet<WindowOperation> Unsupported { get; } = [];

    public WindowOperationResult Result { get; set; } = WindowOperationResult.Ok;

    public IReadOnlyList<(WindowOperation Operation, WindowIdentity Window, WindowSize? Size)> Calls => _calls;

    public bool Supports(WindowOperation operation) => !Unsupported.Contains(operation);

    public WindowOperationResult Perform(WindowOperation operation, WindowIdentity window, WindowSize? size = null)
    {
        _calls.Add((operation, window, size));
        return Result;
    }
}
