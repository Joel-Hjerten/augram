namespace Augram.App.Hosting;

public static class SyncTriggerExtensions
{
    /// <summary>True for the triggers that wait while sync is off, automatic sync is off or a join choice is pending.</summary>
    public static bool IsAutomatic(this SyncTrigger trigger)
        => trigger is SyncTrigger.Start or SyncTrigger.LocalChange or SyncTrigger.Poll or SyncTrigger.AutoSyncOn;
}
