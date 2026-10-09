using Augram.Core.Abstractions;
using Augram.Platform.Windows.Clipboard;
using Xunit;

namespace Augram.Platform.Windows.Tests.Clipboard;

/// <summary>
/// The Windows clipboard adapter over scripted Open/Empty/Close calls: no test opens, empties or waits on the real
/// clipboard. Covers the plain clear, another app holding the clipboard for a while or for good, and a failed empty.
/// </summary>
public sealed class Win32ClipboardTests
{
    [Fact]
    public void ClearOpensEmptiesAndClosesOnce()
    {
        var calls = new Calls();

        var result = calls.Clipboard().Clear();

        Assert.Equal(ClipboardResult.Ok, result);
        Assert.Equal(["open", "empty", "close"], calls.Log);
    }

    [Fact]
    public void AClipboardHeldForAMomentIsRetriedUntilItOpens()
    {
        var calls = new Calls { OpenFailures = 3 };

        var result = calls.Clipboard().Clear();

        Assert.True(result.Succeeded);
        Assert.Equal(["open", "wait", "open", "wait", "open", "wait", "open", "empty", "close"], calls.Log);
        Assert.All(calls.Waits, wait => Assert.Equal(Win32Clipboard.RetryDelay, wait));
    }

    [Fact]
    public void AClipboardHeldForGoodFailsAfterTheLastAttempt_NamingTheError()
    {
        var calls = new Calls { OpenFailures = int.MaxValue, Error = 5 };

        var result = calls.Clipboard().Clear();

        Assert.False(result.Succeeded);
        Assert.True(result.IsSupported);
        Assert.Equal($"the clipboard is held by another app (OpenClipboard failed {Win32Clipboard.Attempts} times, error 5)", result.Reason);
        Assert.Equal(Win32Clipboard.Attempts, calls.Log.Count(call => call == "open"));
        Assert.Equal(Win32Clipboard.Attempts - 1, calls.Waits.Count);
        Assert.DoesNotContain("empty", calls.Log);
        Assert.DoesNotContain("close", calls.Log);
    }

    [Fact]
    public void AFailedEmptyFails_AndTheClipboardIsStillClosed()
    {
        var calls = new Calls { EmptySucceeds = false, Error = 1418 };

        var result = calls.Clipboard().Clear();

        Assert.Equal(ClipboardResult.Failed("EmptyClipboard failed (1418)"), result);
        Assert.Equal(["open", "empty", "close"], calls.Log);
    }

    /// <summary>The scripted Win32 side: how many opens fail first, whether the empty works, the last error.</summary>
    private sealed class Calls
    {
        public int OpenFailures { get; init; }

        public bool EmptySucceeds { get; init; } = true;

        public int Error { get; init; }

        public List<string> Log { get; } = [];

        public List<TimeSpan> Waits { get; } = [];

        public Win32Clipboard Clipboard()
        {
            var failuresLeft = OpenFailures;
            return new Win32Clipboard(
                () =>
                {
                    Log.Add("open");
                    return failuresLeft-- <= 0;
                },
                () =>
                {
                    Log.Add("empty");
                    return EmptySucceeds;
                },
                () =>
                {
                    Log.Add("close");
                    return true;
                },
                () => Error,
                wait =>
                {
                    Log.Add("wait");
                    Waits.Add(wait);
                });
        }
    }
}
