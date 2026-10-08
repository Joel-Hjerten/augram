namespace Augram.Core.Abstractions;

/// <summary>Starts nothing and says so; the default for tests and for a platform without an adapter.</summary>
public sealed class NullProcessLauncher : IProcessLauncher
{
    public const string Reason = "no process launcher on this platform";

    public static NullProcessLauncher Instance { get; } = new();

    private NullProcessLauncher()
    {
    }

    public ProcessLaunchResult Launch(ProcessLaunch launch) => ProcessLaunchResult.NotSupported(Reason);
}
