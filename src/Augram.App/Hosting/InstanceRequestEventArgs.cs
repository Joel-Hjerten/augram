namespace Augram.App.Hosting;

/// <summary>A request that reached the running Augram's <see cref="SingleInstanceGuard"/>, raised on its listener thread.</summary>
public sealed class InstanceRequestEventArgs : EventArgs
{
    public InstanceRequestEventArgs(InstanceIdentity? from, string? reason)
    {
        From = from;
        Reason = reason;
    }

    /// <summary>The launch that asked; null for an Augram from before identities, or a connection that said nothing readable.</summary>
    public InstanceIdentity? From { get; }

    /// <summary>The asking launch's reason ("same install", "take-over cancelled"); null when it gave none.</summary>
    public string? Reason { get; }
}
