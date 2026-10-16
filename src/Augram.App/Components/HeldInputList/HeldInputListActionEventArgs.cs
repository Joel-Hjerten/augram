using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.Components.HeldInputList;

/// <summary>One <see cref="HeldInputListAction"/> with the row it applies to (Select, Copy, Delete, Reorder, CopyTo), the input for Add, the destination for Reorder and the platform for ShowSide.</summary>
public sealed class HeldInputListActionEventArgs : EventArgs
{
    public HeldInputListActionEventArgs(HeldInputListAction action, HeldInputItem? item = null, KeyOrButton? input = null, int targetIndex = -1, HostPlatform? platform = null)
    {
        Action = action;
        Item = item;
        Input = input;
        TargetIndex = targetIndex;
        Platform = platform;
    }

    public HeldInputListAction Action { get; }

    public HeldInputItem? Item { get; }

    public KeyOrButton? Input { get; }

    public int TargetIndex { get; }

    public HostPlatform? Platform { get; }
}
