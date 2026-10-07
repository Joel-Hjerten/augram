namespace Augram.Core.Abstractions;

/// <summary>Supports nothing and performs nothing; the default for tests and for a platform without an adapter yet.</summary>
public sealed class NullWindowOperations : IWindowOperations
{
    public static NullWindowOperations Instance { get; } = new();

    private NullWindowOperations()
    {
    }

    public HostPlatform Platform => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;

    public bool Supports(WindowOperation operation) => false;

    public WindowOperationResult Perform(WindowOperation operation, WindowIdentity window, WindowSize? size = null)
        => WindowOperationResult.NotSupported(operation, Platform);
}
