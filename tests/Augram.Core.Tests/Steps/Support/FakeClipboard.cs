using Augram.Core.Abstractions;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>Fake <see cref="IClipboard"/>: counts the clears and answers <see cref="Answer"/>; never touches the real clipboard.</summary>
internal sealed class FakeClipboard : IClipboard
{
    public ClipboardResult Answer { get; set; } = ClipboardResult.Ok;

    public int Clears { get; private set; }

    public ClipboardResult Clear()
    {
        Clears++;
        return Answer;
    }
}
