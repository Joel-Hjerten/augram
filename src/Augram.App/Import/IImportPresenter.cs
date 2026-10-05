namespace Augram.App.Import;

/// <summary>Runs the StrokesPlus.net import flow (F8): file picker, then the merge dialog. The Gestures view model forwards the intent here; tests substitute a fake.</summary>
public interface IImportPresenter
{
    Task OpenAsync();
}
