using Augram.App.Dialogs;
using Augram.App.ViewModels;

namespace Augram.App.Training;

/// <summary>
/// The semi-large training popup (F3), sized by <see cref="DialogSize.Training"/>: hosts <see cref="TrainingView"/> and closes
/// itself when the session ends. Escape and the close box are Cancel (<see cref="OnClosed"/>), once: the view's Cancel button
/// does not fire as well (<see cref="DialogWindow"/>). Code-only; its one job is to host the view.
/// </summary>
public sealed class TrainingWindow : DialogWindow
{
    private readonly TrainingViewModel _viewModel;
    private bool _ended;

    public TrainingWindow(TrainingViewModel viewModel)
        : base(DialogSize.Training)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        CanResize = true;
        Title = viewModel.Title;
        DataContext = viewModel;
        Content = new TrainingView();
        viewModel.PropertyChanged += (_, _) => Title = viewModel.Title;
        viewModel.Ended += (_, _) =>
        {
            _ended = true;
            Close();
        };
    }

    /// <summary>Escape and the close box are Cancel; the session's own end has already set <see cref="_ended"/>.</summary>
    protected override void OnClosed(EventArgs e)
    {
        if (!_ended)
        {
            _viewModel.Cancel();
        }

        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
