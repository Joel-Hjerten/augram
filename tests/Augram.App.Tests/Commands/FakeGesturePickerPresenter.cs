using Augram.App.Components.GesturePicker;
using Augram.Core.Gestures;

namespace Augram.App.Tests.Commands;

/// <summary>Records what the picker was opened with and answers <see cref="Result"/> at once.</summary>
public sealed class FakeGesturePickerPresenter : IGesturePickerPresenter
{
    public List<GestureId?> Requests { get; } = [];

    public GesturePickerResult Result { get; set; } = GesturePickerResult.Cancelled;

    public Task<GesturePickerResult> PickAsync(GestureId? current)
    {
        Requests.Add(current);
        return Task.FromResult(Result);
    }
}
