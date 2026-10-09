namespace Augram.Core.Abstractions;

/// <summary>
/// The system clipboard as steps see it (the Clear clipboard step; SP.net's <c>clip.Clear()</c>). Implemented by
/// Platform.Windows (<c>OpenClipboard</c> + <c>EmptyClipboard</c>, retried while another app holds it) and Platform.MacOS
/// (<c>NSPasteboard</c> <c>clearContents</c>); <see cref="NullClipboard"/> declines. Called on the command executor thread
/// only; needs no UI thread, returns within a bounded time and never throws for an expected failure.
/// </summary>
public interface IClipboard
{
    /// <summary>Empties the clipboard: nothing is left to paste.</summary>
    ClipboardResult Clear();
}
