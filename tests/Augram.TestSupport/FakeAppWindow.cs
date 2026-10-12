using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>Fake <see cref="IAppWindow"/>: <see cref="Open"/> answers <see cref="Opens"/> and counts the opens that succeeded; shows nothing.</summary>
public sealed class FakeAppWindow : IAppWindow
{
    public bool Opens { get; init; }

    public int Opened { get; private set; }

    public bool Open()
    {
        Opened += Opens ? 1 : 0;
        return Opens;
    }
}
