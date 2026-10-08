namespace Augram.Core.Abstractions;

/// <summary>
/// Outcome of <see cref="IDisplayModes.SetMode"/> or <see cref="IDisplayModes.SetHdr"/>: <see cref="Succeeded"/>, or a
/// <see cref="Reason"/> fit for the log ("Windows refused the mode (DISP_CHANGE_BADMODE)", "display gone").
/// </summary>
public sealed record DisplayChangeResult(bool Succeeded, string? Reason = null)
{
    public static DisplayChangeResult Ok { get; } = new(true);

    public static DisplayChangeResult Failed(string reason) => new(false, reason);
}
