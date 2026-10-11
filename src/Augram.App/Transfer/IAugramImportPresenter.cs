namespace Augram.App.Transfer;

/// <summary>
/// Runs an Augram file import (requirements F8, plan 0003 step 4): the open picker, the read (a file that cannot be read or
/// is refused is said in a message), the review, the apply and its summary. Answers the line for the caller's status ("Imported
/// from Blender.augram.json: …", or why the file was not imported), or null when the user cancelled. Options › Sync › Export
/// and import calls it; tests substitute a fake.
/// </summary>
public interface IAugramImportPresenter
{
    Task<string?> OpenAsync();
}
