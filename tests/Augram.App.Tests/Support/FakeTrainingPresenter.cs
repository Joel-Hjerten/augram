using Augram.App.Training;

namespace Augram.App.Tests.Support;

public sealed class FakeTrainingPresenter : ITrainingPresenter
{
    public List<TrainingRequest> Requests { get; } = [];

    public void Open(TrainingRequest request) => Requests.Add(request);
}
