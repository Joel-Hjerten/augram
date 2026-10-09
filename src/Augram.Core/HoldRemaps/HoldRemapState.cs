namespace Augram.Core.HoldRemaps;

/// <summary>Where a <see cref="HoldRemapMachine"/> is.</summary>
public enum HoldRemapState
{
    /// <summary>No hold key held and nothing of a hold remap still held.</summary>
    Idle,

    /// <summary>A hold key is held: its inputs are remapped.</summary>
    Holding,

    /// <summary>A hold key is held but the user is typing (the rollover sent it already): everything passes until it is released.</summary>
    RolledOver,

    /// <summary>The hold key is up, but input buttons or keys pressed during the hold are still down: the remap follows them until the last is up.</summary>
    Following,
}
