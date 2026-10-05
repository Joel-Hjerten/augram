namespace Augram.Core.Config;

/// <summary>
/// Modifier keys that, when held while the stroke button goes down, make Augram pass the
/// button through untouched (the Ignore Key, requirements F1). Combinable: any held
/// member of the set ignores.
/// </summary>
[Flags]
public enum IgnoreKeys
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}
