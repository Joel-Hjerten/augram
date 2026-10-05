using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>A cursor the test moves. <see cref="Available"/> false fakes a failed <c>GetCursorPos</c>.</summary>
internal sealed class FakeCursorProbe : ICursorProbe
{
    public int X { get; set; }

    public int Y { get; set; }

    public bool Available { get; set; } = true;

    public bool TryGetPosition(out int x, out int y)
    {
        x = X;
        y = Y;
        return Available;
    }
}
