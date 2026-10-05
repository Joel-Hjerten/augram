using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Training;

/// <summary>Content of the training popup; wires the draw area's two events to the view model and nothing else.</summary>
public sealed partial class TrainingView : UserControl
{
    public TrainingView()
    {
        InitializeComponent();
        DrawArea.StrokeCompleted += (_, points) => ViewModel?.StrokeDrawn(points);
        DrawArea.ScreenAreaChanged += (_, area) => ViewModel?.CanvasMoved(area);
    }

    private TrainingViewModel? ViewModel => DataContext as TrainingViewModel;
}
