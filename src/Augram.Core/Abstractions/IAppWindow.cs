namespace Augram.Core.Abstractions;

/// <summary>
/// Augram's own window, for the Open app step's "This app (Augram)" (Joel, 2026-10-09): opens and focuses it as a double
/// click on the tray or menu-bar icon does. The App implements it by posting to the UI thread, so <see cref="Open"/> returns
/// at once; false when there is no window to open (a host without UI, a test).
/// </summary>
public interface IAppWindow
{
    bool Open();
}

public sealed class NullAppWindow : IAppWindow
{
    public static NullAppWindow Instance { get; } = new();

    public bool Open() => false;
}
