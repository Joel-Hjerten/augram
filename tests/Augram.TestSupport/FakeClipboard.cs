using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>
/// Fake <see cref="IClipboard"/>: counts the clears, keeps the managed thread id of each in <see cref="ClearedOn"/> (to prove
/// which thread ran the step) and answers <see cref="Answer"/>; never touches the real clipboard.
/// </summary>
public sealed class FakeClipboard : IClipboard
{
    private readonly List<int> _clearedOn = [];

    public ClipboardResult Answer { get; set; } = ClipboardResult.Ok;

    public int Clears => _clearedOn.Count;

    public IReadOnlyList<int> ClearedOn => _clearedOn;

    public ClipboardResult Clear()
    {
        _clearedOn.Add(Environment.CurrentManagedThreadId);
        return Answer;
    }
}
