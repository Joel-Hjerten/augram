using System.IO.Pipes;

namespace Augram.App.Hosting.Instance;

/// <summary>
/// The asking side of one Augram at a time (A10): a launch that finds the name taken talks to the running one's
/// <see cref="InstanceListener"/> (wire format: <see cref="InstanceProtocol"/>). Hello returns the running one's identity, Show
/// shows its window, Quit makes it shut down the way Quit in the tray does; <see cref="InstanceStartup"/> decides which to send.
/// A 0.1 listener that refuses a two-way connection is asked the old way. BCL only.
/// </summary>
public static class InstanceClient
{
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
            using var client = new NamedPipeClientStream(".", InstancePipe.PipeNameFor(name), PipeDirection.InOut, InstancePipe.Flags);
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
            using var client = new NamedPipeClientStream(".", InstancePipe.PipeNameFor(name), PipeDirection.Out, InstancePipe.Flags);
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

    private static int Milliseconds(TimeSpan timeout) => (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue);
}
