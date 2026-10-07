namespace Augram.Core.Abstractions;

/// <summary>
/// Outcome of <see cref="IWindowOperations.Perform"/>: <see cref="Succeeded"/>, or a <see cref="Reason"/> fit
/// for the log ("not supported on macOS", "window gone", "SetWindowPos failed (5)").
/// </summary>
public sealed record WindowOperationResult(bool Succeeded, string? Reason = null)
{
    public static WindowOperationResult Ok { get; } = new(true);

    public static WindowOperationResult Failed(string reason) => new(false, reason);

    public static WindowOperationResult NotSupported(WindowOperation operation, HostPlatform platform)
        => new(false, $"{operation} is not supported on {platform}");
}
