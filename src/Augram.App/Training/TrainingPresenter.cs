using Augram.App.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Training;

/// <summary>
/// Shows the <see cref="TrainingSession"/> as a <see cref="TrainingWindow"/> owned by the main window
/// (F3: semi-large, centred, Cancel and Accept). One window at a time: opening while one is up
/// cancels the previous session and starts the new one; the window closes itself when the session ends.
/// </summary>
public sealed class TrainingPresenter : ITrainingPresenter
{
    private readonly TrainingSession _session;
    private TrainingWindow? _window;

    public TrainingPresenter(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public void Open(TrainingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _window?.Close();
        _session.Begin(request);
        var window = new TrainingWindow(new TrainingViewModel(_session));
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        _window = window;

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        window.Activate();
    }
}
