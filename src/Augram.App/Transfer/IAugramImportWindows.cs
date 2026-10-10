using Augram.App.ViewModels;

namespace Augram.App.Transfer;

/// <summary>
/// The windows an import shows (plan 0003 step 4): the review, modal until it is imported or cancelled, and a message with one
/// OK button (a file that cannot be imported; the summary afterwards). <see cref="AugramImportWindows"/> shows them over the
/// main window; tests substitute a fake that answers the review as a user would.
/// </summary>
public interface IAugramImportWindows
{
    Task ShowReviewAsync(AugramImportViewModel review);

    Task ShowMessageAsync(string title, string message);
}
