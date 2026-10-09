namespace Augram.Core.Abstractions;

/// <summary>Knows no windows and activates nothing; the default for tests and for a platform without an adapter yet.</summary>
public sealed class NullWindowSystem : IWindowSystem
{
    public static NullWindowSystem Instance { get; } = new();

    private NullWindowSystem()
    {
    }

    public WindowIdentity? WindowAt(int x, int y) => null;

    public WindowIdentity? Foreground() => null;

    public nint? WindowKeyAt(int x, int y) => 0;

    public nint? ForegroundKey() => 0;

    public ActivationResult Activate(WindowIdentity target) => ActivationResult.NotNeeded;
}
