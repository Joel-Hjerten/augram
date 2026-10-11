using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Augram.App.Hosting.Instance;

/// <summary>
/// The names and options both sides of one-Augram-at-a-time agree on (<see cref="InstanceListener"/>, <see cref="InstanceClient"/>):
/// the mutex's name and scope, the pipe's name and its options. BCL only.
/// </summary>
internal static class InstancePipe
{
    private const int MaxUnixPipeName = 24;

    /// <summary>
    /// Mutex scope: one instance per user. On Windows that is also per logon session (<c>Local\</c>). Elsewhere a session is a
    /// terminal session, and every launch from the Dock, Finder or another terminal starts a new one, so a session-scoped
    /// mutex let a second Augram start beside the first on macOS (2026-10-07, two hooks on one mouse).
    /// </summary>
    internal static NamedWaitHandleOptions MutexScope => new() { CurrentUserOnly = true, CurrentSessionOnly = OperatingSystem.IsWindows() };

    /// <summary>
    /// Windows pipe names are machine-wide: the pipe takes connections only from this user, and a client connects only to this
    /// user's pipe, so another user's Augram can never be shown or quit. Unix pipes live in the per-user temp folder.
    /// </summary>
    internal static PipeOptions Flags => PipeOptions.Asynchronous | (OperatingSystem.IsWindows() ? PipeOptions.CurrentUserOnly : PipeOptions.None);

    internal static string MutexNameFor(string name) => name + ".instance";

    /// <summary>
    /// Off Windows a named pipe is a domain socket at <c>$TMPDIR/CoreFxPipe_&lt;name&gt;</c>, and macOS caps that path at
    /// 104 characters (<c>$TMPDIR</c> alone is about 50), so a long instance name is shortened to a hash of itself.
    /// </summary>
    internal static string PipeNameFor(string name)
    {
        var pipe = name + ".show";
        if (OperatingSystem.IsWindows() || pipe.Length <= MaxUnixPipeName)
        {
            return pipe;
        }

        return "augram-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pipe)).AsSpan(0, 8));
    }
}
