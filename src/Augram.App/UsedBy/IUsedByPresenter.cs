using Augram.Core.Gestures;

namespace Augram.App.UsedBy;

/// <summary>Opens the "Used by…" popup for a gesture (F3). The Gestures view model forwards the intent here; tests substitute a fake.</summary>
public interface IUsedByPresenter
{
    void Show(GestureId gestureId);
}
