namespace Augram.Core.Abstractions;

/// <summary>
/// Outcome of an <see cref="IClipboard"/> call: <see cref="Succeeded"/>, or a <see cref="Reason"/> fit for the log
/// ("the clipboard is held by another app (error 5)"). <see cref="IsSupported"/> is false when the platform has no
/// clipboard adapter at all, which a step reports as skipped rather than failed.
/// </summary>
public sealed record ClipboardResult(bool Succeeded, string? Reason = null, bool IsSupported = true)
{
    public static ClipboardResult Ok { get; } = new(true);

    public static ClipboardResult Failed(string reason) => new(false, reason);

    public static ClipboardResult NotSupported(string reason) => new(false, reason, IsSupported: false);
}
