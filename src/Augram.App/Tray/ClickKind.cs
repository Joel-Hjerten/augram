namespace Augram.App.Tray;

public enum ClickKind
{
    None,

    /// <summary>A click that may still become a double click; call <see cref="ClickDiscriminator.Flush"/> after the window.</summary>
    Pending,

    Single,

    Double,
}
