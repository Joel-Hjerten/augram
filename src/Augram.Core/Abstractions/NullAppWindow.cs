namespace Augram.Core.Abstractions;

/// <summary>No window to open: a host without UI, and tests.</summary>
public sealed class NullAppWindow : IAppWindow
{
    public static NullAppWindow Instance { get; } = new();

    public bool Open() => false;
}
