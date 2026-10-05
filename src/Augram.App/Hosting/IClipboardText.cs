namespace Augram.App.Hosting;

/// <summary>Writes text to the system clipboard. View models take this so they never reach for a window (ADR-0002 §5a).</summary>
public interface IClipboardText
{
    Task SetTextAsync(string text);
}
