namespace Augram.Core.Abstractions;

/// <summary>
/// Outcome of one <see cref="ISyncRepository"/> call: <see cref="Succeeded"/>, or an <see cref="Error"/>
/// line fit for the log and the Options page ("git is not installed", "sign-in needed: run a push once
/// from a terminal", "offline"). Never contains a credential or a credentialed URL.
/// </summary>
public sealed record SyncOperationResult(bool Succeeded, string? Error = null)
{
    public static SyncOperationResult Ok { get; } = new(true);

    public static SyncOperationResult Failed(string error) => new(false, error);
}
