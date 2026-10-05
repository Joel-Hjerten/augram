using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Training;

public sealed partial class TrainingWindow : Window
{
    private readonly TrainingViewModel _viewModel;
    private bool _ended;

    /// <summary>For the XAML previewer and runtime loader only.</summary>
    public TrainingWindow()
        : this(new TrainingViewModel(new TrainingSession(new Core.Gestures.GestureLibrary(), () => Core.Recognition.RecognitionOptions.Default)))
    {
    }

    public TrainingWindow(TrainingViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Ended += (_, _) =>
        {
            _ended = true;
            Close();
        };
    }

    /// <summary>The close button is Cancel; the session's own end has already set <see cref="_ended"/>.</summary>
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
