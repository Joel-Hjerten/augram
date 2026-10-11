namespace Augram.Core.Abstractions;

/// <summary>Leaves every app to the launcher: macOS (<c>open -a</c> activates a running app) and tests.</summary>
public sealed class NullAppActivator : IAppActivator
{
    public static NullAppActivator Instance { get; } = new();

    public AppActivation BringToFront(string executable) => AppActivation.NotRunning;
}
