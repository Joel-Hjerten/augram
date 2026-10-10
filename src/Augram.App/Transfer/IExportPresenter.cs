using Augram.Core.Transfer;

namespace Augram.App.Transfer;

/// <summary>
/// Runs an export (requirements F8, plan 0003 step 3): the scope dialog starting on the caller's preselection,
/// the save picker, the write. Answers the line to show where it was started from ("Exported Blender: 1 app group, 8 commands,
/// 2 gestures to Augram Blender 2026-10-10.augram.json." or why it could not save), or null when the user cancelled. Options,
/// the Commands tab's menu and the Gestures toolbar call it; tests substitute a fake.
/// </summary>
public interface IExportPresenter
{
    /// <param name="start">The scope the dialog starts on: everything (Options), gestures only (the Gestures toolbar), or one group (a group's or a category's menu).</param>
    Task<string?> ExportAsync(ExportScope start);
}
