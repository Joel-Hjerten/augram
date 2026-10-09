using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;

namespace Augram.Platform.Windows.Clipboard;

/// <summary>
/// The Windows <see cref="IClipboard"/> (the Clear clipboard step): <c>OpenClipboard(NULL)</c>, <c>EmptyClipboard</c>,
/// and <c>CloseClipboard</c> whatever happened. Only one process holds the clipboard at a time, so opening it fails
/// while another app reads or writes it; that is retried <see cref="Attempts"/> times, <see cref="RetryDelay"/> apart
/// (≈ 200 ms at most on the command executor thread), then reported with the last Win32 error. No window and no message
/// loop are needed, so it runs on the executor thread. Nothing here throws.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32Clipboard : IClipboard
{
    public const int Attempts = 10;

    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);

    private readonly Func<bool> _open;
    private readonly Func<bool> _empty;
    private readonly Func<bool> _close;
    private readonly Func<int> _lastError;
    private readonly Action<TimeSpan> _wait;

    public Win32Clipboard()
        : this(() => NativeMethods.OpenClipboard(0), NativeMethods.EmptyClipboard, NativeMethods.CloseClipboard, Marshal.GetLastPInvokeError, Thread.Sleep)
    {
    }

    /// <param name="open">Opens the clipboard for this thread; false while another app holds it.</param>
    /// <param name="empty">Empties the open clipboard.</param>
    /// <param name="close">Closes it again.</param>
    /// <param name="lastError">The Win32 error of the call just made.</param>
    /// <param name="wait">Waits between attempts; tests pass a recorder, so no test sleeps or touches the real clipboard.</param>
    internal Win32Clipboard(Func<bool> open, Func<bool> empty, Func<bool> close, Func<int> lastError, Action<TimeSpan> wait)
    {
        _open = open;
        _empty = empty;
        _close = close;
        _lastError = lastError;
        _wait = wait;
    }

    public ClipboardResult Clear()
    {
        var error = 0;
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            if (_open())
            {
                return EmptyAndClose();
            }

            error = _lastError();
            if (attempt < Attempts)
            {
                _wait(RetryDelay);
            }
        }

        return ClipboardResult.Failed($"the clipboard is held by another app (OpenClipboard failed {Attempts} times, error {error})");
    }

    private ClipboardResult EmptyAndClose()
    {
        try
        {
            return _empty() ? ClipboardResult.Ok : ClipboardResult.Failed($"EmptyClipboard failed ({_lastError()})");
        }
        finally
        {
            _close();
        }
    }
}
