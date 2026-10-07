using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Augram.App.Hosting;

/// <summary>
/// One running Augram per user (A10). The first launch owns a named <see cref="Mutex"/> and listens on
/// a named pipe; a second launch finds the mutex taken, connects to the pipe to ask the first to show
/// its window, and exits. BCL only; pipes work on Windows and Unix alike. <see cref="ShowRequested"/>
/// is raised on the listener thread, so the subscriber marshals to the UI.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const int MaxUnixPipeName = 24;
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _listener;

    private SingleInstanceGuard(string name, Mutex mutex)
    {
        _mutex = mutex;
        _pipeName = PipeNameFor(name);
        _listener = new Thread(Listen) { IsBackground = true, Name = "augram-single-instance" };
        _listener.Start();
    }

    public event EventHandler? ShowRequested;

    /// <summary>Null when another instance already owns <paramref name="name"/>.</summary>
    public static SingleInstanceGuard? TryAcquire(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var mutex = new Mutex(initiallyOwned: true, name + ".instance", MutexScope, out var createdNew);
        if (createdNew)
        {
            return new SingleInstanceGuard(name, mutex);
        }

        mutex.Dispose();
        return null;
    }

    /// <summary>Asks the instance that owns <paramref name="name"/> to show itself. False when none answered in time.</summary>
    public static bool SignalExisting(string name, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        try
        {
            using var client = new NamedPipeClientStream(".", PipeNameFor(name), PipeDirection.Out);
            client.Connect((int)timeout.TotalMilliseconds);
            client.WriteByte(1);
            client.Flush();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _mutex.Dispose();
        _stopping.Dispose();
    }

    /// <summary>
    /// One instance per user. On Windows that is also per logon session (<c>Local\</c>). Elsewhere a session is a terminal
    /// session, and every launch from the Dock, Finder or another terminal starts a new one, so a session-scoped mutex let
    /// a second Augram start beside the first on macOS (2026-10-07, two hooks on one mouse).
    /// </summary>
    private static NamedWaitHandleOptions MutexScope => new() { CurrentUserOnly = true, CurrentSessionOnly = OperatingSystem.IsWindows() };

    /// <summary>
    /// Off Windows a named pipe is a domain socket at <c>$TMPDIR/CoreFxPipe_&lt;name&gt;</c>, and macOS caps that path at
    /// 104 characters (<c>$TMPDIR</c> alone is about 50), so a long instance name is shortened to a hash of itself.
    /// </summary>
    private static string PipeNameFor(string name)
    {
        var pipe = name + ".show";
        if (OperatingSystem.IsWindows() || pipe.Length <= MaxUnixPipeName)
        {
            return pipe;
        }

        return "augram-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pipe)).AsSpan(0, 8));
    }

    private void Listen()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                server.WaitForConnectionAsync(_stopping.Token).GetAwaiter().GetResult();
                server.ReadByte();
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client dropped mid-handshake; listen again.
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e) when (e is ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // The pipe cannot exist here. A second launch then cannot reach this one, but an exception escaping this thread would end the app.
                return;
            }
        }
    }
}
