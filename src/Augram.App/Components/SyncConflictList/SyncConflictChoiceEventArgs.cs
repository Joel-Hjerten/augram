using Augram.Core.Sync;

namespace Augram.App.Components.SyncConflictList;

/// <summary>The user picked <see cref="Choice"/> for the conflict at <see cref="Index"/> in the list's entries.</summary>
public sealed class SyncConflictChoiceEventArgs : EventArgs
{
    public SyncConflictChoiceEventArgs(int index, SyncChoice choice)
    {
        Index = index;
        Choice = choice;
    }

    public int Index { get; }

    public SyncChoice Choice { get; }
}
