using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting;

/// <summary>
/// Process-wide flags the tray and the Options page share: whether gestures are on, and start at
/// login. In-memory for M1 step 6; the engine host (step 4b) takes over <see cref="Enabled"/> and the
/// settings store takes over <see cref="StartAtLogin"/>, each a one-place change here.
/// </summary>
public sealed partial class AppState : ObservableObject
{
    [ObservableProperty]
    public partial bool Enabled { get; set; } = true;

    [ObservableProperty]
    public partial bool StartAtLogin { get; set; }

    public void Toggle() => Enabled = !Enabled;
}
