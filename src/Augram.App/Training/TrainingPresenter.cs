using Augram.App.Dialogs;
using Augram.App.ViewModels;
using Augram.Core.Training;

namespace Augram.App.Training;

/// <summary>
/// Shows the <see cref="TrainingSession"/> as a <see cref="TrainingWindow"/> owned by the main window
/// (F3: semi-large, centred, Cancel and Accept). One window at a time (<see cref="DialogSlot"/>): opening while one is up
/// cancels the previous session and starts the new one; the window closes itself when the session ends.
/// </summary>
public sealed class TrainingPresenter : ITrainingPresenter
{
    private readonly TrainingSession _session;
    private readonly DialogSlot _slot = new();

    public TrainingPresenter(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public void Open(TrainingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Closing the previous window cancels its session; it must happen before the new one begins.
        _slot.Close();
        _session.Begin(request);
        _slot.Show(new TrainingWindow(new TrainingViewModel(_session)));
    }
}
