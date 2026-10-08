using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Augram.App.Hosting;

/// <summary>
/// One running Augram per user (A10), whichever build: never the installed and a development one at once (Joel,
/// 2026-10-08), so both use the same name. The running Augram owns a named mutex (held by a thread of its own,
/// <see cref="NamedMutexOwner"/>, so a take-over can wait for it from any thread) and listens on a named pipe. A second
/// launch finds the mutex taken and talks to the pipe with <see cref="Send"/> (wire format: <see cref="InstanceProtocol"/>):
/// Hello returns the running one's identity, Show shows its window, Quit makes it shut down the way Quit in the tray does;
/// <see cref="InstanceStartup"/> decides which to send. Every connection that is not a readable request (Augram 0.1's one
/// byte, a dropped connection) is a show request, and a 0.1 listener that refuses a two-way connection is asked the old way.
/// The events are raised on the listener thread, so subscribers marshal to the UI. BCL only; pipes work on Windows and Unix
/// alike (on Windows the pipe accepts and reaches only the same user: the pipe name is machine-wide there).
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>How long the listener waits for a request's bytes and for the asker to take the answer.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private const int MaxUnixPipeName = 24;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly NamedMutexOwner _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _listener;
    private int _disposed;

    private SingleInstanceGuard(string name, InstanceIdentity identity, NamedMutexOwner mutex)
    {
        Identity = identity;
        _mutex = mutex;
        _pipeName = PipeNameFor(name);
        _listener = new Thread(Listen) { IsBackground = true, Name = "augram-single-instance" };
        _listener.Start();
    }

    /// <summary>Another launch asked who is running (it may ask the user next); nothing visible should happen.</summary>
    public event EventHandler<InstanceRequestEventArgs>? Introduced;

    /// <summary>Show the main window: a second launch of the same install, a cancelled take-over, or an older Augram.</summary>
    public event EventHandler<InstanceRequestEventArgs>? ShowRequested;

    /// <summary>Another build was chosen to run instead: shut down as Quit in the tray does, so the mutex is released.</summary>
    public event EventHandler<InstanceRequestEventArgs>? QuitRequested;

    /// <summary>The identity this listener answers with.</summary>
    public InstanceIdentity Identity { get; }

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
    private static PipeOptions PipeFlags => PipeOptions.Asynchronous | (OperatingSystem.IsWindows() ? PipeOptions.CurrentUserOnly : PipeOptions.None);

    /// <summary>Null when another instance already owns <paramref name="name"/>.</summary>
    public static SingleInstanceGuard? TryAcquire(string name, InstanceIdentity identity) =>
        AcquireAsync(name, identity, TimeSpan.Zero).GetAwaiter().GetResult();

    /// <summary>Waits up to <paramref name="wait"/> for <paramref name="name"/> (a take-over waits for the running one to quit); null when it stayed taken.</summary>
    public static async Task<SingleInstanceGuard?> AcquireAsync(string name, InstanceIdentity identity, TimeSpan wait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(identity);
        var mutex = await NamedMutexOwner.AcquireAsync(MutexNameFor(name), MutexScope, wait).ConfigureAwait(false);
        return mutex is null ? null : new SingleInstanceGuard(name, identity, mutex);
    }

    /// <summary>
    /// Sends <paramref name="request"/> to the instance that owns <paramref name="name"/> and returns its answer. Never throws
    /// for a missing, slow or older listener: <see cref="PeerAnswerKind.NoAnswer"/> when none answered in time,
    /// <see cref="PeerAnswerKind.Older"/> when an Augram from before identities took the connection (it shows its window).
    /// </summary>
    public static PeerAnswer Send(string name, InstanceRequest request, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(request);
        var connected = false;
        try
        {
            using var client = new NamedPipeClientStream(".", PipeNameFor(name), PipeDirection.InOut, PipeFlags);
            client.Connect(Milliseconds(timeout));
            connected = true;
            client.Write(InstanceProtocol.EncodeRequest(request));
            client.Flush();
            using var cancel = new CancellationTokenSource(timeout);
            var line = InstanceProtocol.ReadLineAsync(client, cancel.Token).GetAwaiter().GetResult();

            // No answer line: a 0.1 listener read its one byte and closed (it showed its window on connecting).
            return line is not null && InstanceProtocol.DecodeIdentity(line) is { } running
                ? new PeerAnswer(PeerAnswerKind.Answered, running)
                : PeerAnswer.Older;
        }
        catch (UnauthorizedAccessException) when (!connected)
        {
            // A 0.1 listener is inbound only, and Windows refuses a two-way connection to it: ask it the old way.
            return SignalExisting(name, timeout) ? PeerAnswer.Older : PeerAnswer.NoAnswer;
        }
        catch (TimeoutException)
        {
            return PeerAnswer.NoAnswer;
        }
        catch (OperationCanceledException)
        {
            return PeerAnswer.NoAnswer;
        }
        catch (IOException)
        {
            // Broken after connecting: a 0.1 listener closed on us (it showed its window on connecting); before: nobody there.
            return connected ? PeerAnswer.Older : PeerAnswer.NoAnswer;
        }
    }

    /// <summary>
    /// Asks the instance that owns <paramref name="name"/> to show itself the way Augram 0.1 did: one byte, no answer. Kept for a
    /// 0.1 listener (<see cref="Send"/> falls back to it) and for tests of an old second launch. False when none answered in time.
    /// </summary>
    public static bool SignalExisting(string name, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        try
        {
            using var client = new NamedPipeClientStream(".", PipeNameFor(name), PipeDirection.Out, PipeFlags);
            client.Connect(Milliseconds(timeout));
            client.WriteByte(InstanceProtocol.LegacyShow);
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
        catch (UnauthorizedAccessException)
        {
            // Another user's pipe.
            return false;
        }
    }

    /// <summary>Stops listening, then releases the mutex, so whoever takes the name next can open the pipe.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
        var onListener = Thread.CurrentThread == _listener;
        var stopped = !onListener && _listener.Join(StopTimeout);
        _mutex.Dispose();
        if (stopped)
        {
            _stopping.Dispose();
        }
    }

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

    private static int Milliseconds(TimeSpan timeout) => (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue);

    private void Listen()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeFlags);
                server.WaitForConnectionAsync(_stopping.Token).GetAwaiter().GetResult();
                ServeAsync(server).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client dropped mid-handshake, or the previous owner's pipe is still closing: listen again after a breath.
                if (_stopping.Token.WaitHandle.WaitOne(RetryDelay))
                {
                    return;
                }
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

    private async Task ServeAsync(NamedPipeServerStream server)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        timeout.CancelAfter(RequestTimeout);
        var request = await ReadRequestAsync(server, timeout.Token).ConfigureAwait(false);
        if (request is null)
        {
            // Augram 0.1's one byte, or a connection that closed or broke before a full request: the connection is the request.
            // Reading after a 0.1 launch closed can fail as a broken pipe, which must not lose it (a Windows CI run did, 2026-10-07).
            ShowRequested?.Invoke(this, new InstanceRequestEventArgs(null, null));
            return;
        }

        var args = new InstanceRequestEventArgs(request.From, request.Reason);
        switch (request.Kind)
        {
            case InstanceRequestKind.Hello:
                Introduced?.Invoke(this, args);
                await AnswerAsync(server, timeout.Token).ConfigureAwait(false);
                break;
            case InstanceRequestKind.Quit:
                // Answer first: the asker then waits for the mutex, which this instance releases last when it has shut down.
                await AnswerAsync(server, timeout.Token).ConfigureAwait(false);
                QuitRequested?.Invoke(this, args);
                break;
            default:
                ShowRequested?.Invoke(this, args);
                await AnswerAsync(server, timeout.Token).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>The request, or null when the connection did not carry a readable one in time.</summary>
    private async Task<InstanceRequest?> ReadRequestAsync(Stream server, CancellationToken cancellation)
    {
        try
        {
            if (await InstanceProtocol.ReadByteAsync(server, cancellation).ConfigureAwait(false) != InstanceProtocol.Marker)
            {
                return null;
            }

            var line = await InstanceProtocol.ReadLineAsync(server, cancellation).ConfigureAwait(false);
            return line is null ? null : InstanceProtocol.DecodeRequest(line);
        }
        catch (OperationCanceledException) when (!_stopping.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Writes this instance's identity and waits for the asker to close, so the answer is read before the pipe goes.</summary>
    private async Task AnswerAsync(Stream server, CancellationToken cancellation)
    {
        try
        {
            await server.WriteAsync(InstanceProtocol.EncodeIdentity(Identity), cancellation).ConfigureAwait(false);
            await server.FlushAsync(cancellation).ConfigureAwait(false);
            await InstanceProtocol.WaitForCloseAsync(server, cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!_stopping.IsCancellationRequested)
        {
            // The asker kept the pipe open past the timeout; it had its answer.
        }
        catch (IOException)
        {
            // The asker closed (a broken pipe on the last read is its normal goodbye on Windows).
        }
    }
}
