using Augram.Core.Gestures;

namespace Augram.Core.Tests.Fixtures;

/// <summary>Builds <see cref="Gesture"/> records from raw point lists.</summary>
internal static class TestGestures
{
    public static Gesture Create(string name, params IReadOnlyList<GesturePoint>[] samples)
        => Create(name, isActive: true, samples);

    public static Gesture Create(string name, bool isActive, params IReadOnlyList<GesturePoint>[] samples)
        => new(GestureId.New(), name, isActive, samples.Select(points => new GestureSample(points)).ToArray());
}
