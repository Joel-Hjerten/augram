using System.IO.Pipes;
using Augram.App.Hosting;

namespace Augram.App.Tests.Support;

/// <summary>
/// Shared pieces for the single-instance tests. Every test takes a unique name, so the real mutex and pipe are used without
/// ever touching the app's own ("Augram"). Waits block on events with a generous timeout that only a failing test reaches.
/// </summary>
internal static class InstanceTesting
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    public static string UniqueName() => "AugramTest-" + Guid.NewGuid().ToString("N");

    /// <summary>The installed build, as a Velopack install would run it.</summary>
    public static InstanceIdentity Installed { get; } = new(TestBuilds.Release, ExecutableIn("installed"));

    /// <summary>A development build from the repository's Debug output.</summary>
    public static InstanceIdentity Dev { get; } = new(TestBuilds.Dev, ExecutableIn("repo-debug"));

    /// <summary>Another development copy (another worktree, or the Release configuration).</summary>
    public static InstanceIdentity OtherDev { get; } = new(TestBuilds.Dev, ExecutableIn("worktree-debug"));

    public static string ExecutableIn(string folder) => Path.Combine(Path.GetTempPath(), "augram-instance-tests", folder, "Augram.App.exe");

    /// <summary>Holds <paramref name="name"/>'s mutex the way a running Augram does, with no pipe: a silent or an old Augram.</summary>
    public static NamedMutexOwner HoldName(string name) =>
        NamedMutexOwner.AcquireAsync(SingleInstanceGuard.MutexNameFor(name), SingleInstanceGuard.MutexScope, TimeSpan.Zero).GetAwaiter().GetResult()
        ?? throw new InvalidOperationException("the test name was taken");

    /// <summary>Augram 0.1's second launch, byte for byte: an outbound pipe with no options, the byte 1, close.</summary>
    public static void SignalLikeAugram01(string name)
    {
        using var client = new NamedPipeClientStream(".", SingleInstanceGuard.PipeNameFor(name), PipeDirection.Out);
        client.Connect(5000);
        client.WriteByte(1);
        client.Flush();
    }

    /// <summary>
    /// Augram 0.1's listener: inbound only, raises "show" on every connection before reading its one byte. Completes when a
    /// connection arrived; the test cancels it otherwise.
    /// </summary>
    public static Task<bool> ListenLikeAugram01(string name, CancellationToken cancellation) => Task.Run(async () =>
    {
        using var server = new NamedPipeServerStream(SingleInstanceGuard.PipeNameFor(name), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
        try
        {
            server.ReadByte();
        }
        catch (IOException)
        {
            // Already closed by the second launch.
        }

        return true;
    });
}
