namespace Augram.App.Hosting;

/// <summary>What a second launch asks the running Augram over the single-instance pipe (<see cref="SingleInstanceGuard.Send"/>).</summary>
/// <param name="Kind">Hello (who are you), Show (show your window) or Quit (shut down so I can start).</param>
/// <param name="From">The launch that asks.</param>
/// <param name="Reason">Why, for the running one's log: "same install", "take-over cancelled".</param>
public sealed record InstanceRequest(InstanceRequestKind Kind, InstanceIdentity From, string? Reason = null);

/// <summary>The verbs of the single-instance protocol; the running Augram answers each with its own identity.</summary>
public enum InstanceRequestKind
{
    /// <summary>Only asks who is running; nothing visible happens.</summary>
    Hello,

    /// <summary>Show the main window (what every second launch asked before the protocol had identities).</summary>
    Show,

    /// <summary>Shut down cleanly, as Quit in the tray does, so the asking build can start.</summary>
    Quit,
}
