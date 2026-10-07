using Augram.App.Training;
using Augram.Core.Gestures;

namespace Augram.App.Tests.Support;

/// <summary>An <see cref="ITrainingSession"/> that claims every stroke when <see cref="Claims"/> is set and records what it was offered.</summary>
public sealed class FakeTrainingSession : ITrainingSession
{
    public bool Claims { get; set; } = true;

    public List<(IReadOnlyList<GesturePoint> Points, int StartX, int StartY)> Offered { get; } = [];

    public bool TryConsume(IReadOnlyList<GesturePoint> points, int startX, int startY)
    {
        Offered.Add((points, startX, startY));
        return Claims;
    }
}
