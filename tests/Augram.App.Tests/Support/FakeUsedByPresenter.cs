using Augram.App.UsedBy;
using Augram.Core.Gestures;

namespace Augram.App.Tests.Support;

public sealed class FakeUsedByPresenter : IUsedByPresenter
{
    public List<GestureId> Shown { get; } = [];

    public void Show(GestureId gestureId) => Shown.Add(gestureId);
}
