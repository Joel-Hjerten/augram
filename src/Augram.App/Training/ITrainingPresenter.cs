namespace Augram.App.Training;

/// <summary>Opens the training popup for a request (F3). The Gestures view model forwards the intent here; tests substitute a fake.</summary>
public interface ITrainingPresenter
{
    void Open(TrainingRequest request);
}
