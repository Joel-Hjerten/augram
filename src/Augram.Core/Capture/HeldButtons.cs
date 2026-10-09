namespace Augram.Core.Capture;

/// <summary>
/// Mouse buttons as a set: a trigger's "while holding" buttons (F1 "Triggers as combinations", Joel 2026-10-09) and what a
/// press saw held. <see cref="Stroke"/> is whichever button is the stroke button on the machine at hand (a per-machine
/// setting, so a synced trigger keeps meaning "the stroke button"); the other members are physical buttons. Stored by name
/// in the config (flags comma-separated), so the names may change only with a migration.
/// </summary>
[Flags]
public enum HeldButtons
{
    None = 0,

    /// <summary>The stroke button, whichever physical button it is on this machine.</summary>
    Stroke = 1,

    Left = 2,
    Middle = 4,
    Right = 8,
    X1 = 16,
    X2 = 32,
}
