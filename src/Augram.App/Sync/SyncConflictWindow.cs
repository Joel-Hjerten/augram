using Augram.App.Components.SyncConflictList;
using Augram.App.Dialogs;
using Augram.App.ViewModels;

namespace Augram.App.Sync;

/// <summary>
/// The conflict dialog (F8 sync): in the <see cref="DialogShell"/>, a heading ("2 conflicts with Mac") with its ⓘ, the
/// <see cref="SyncConflictList"/> over <see cref="SyncConflictsViewModel.Entries"/> (a choice per conflict, kept by
/// the view model) and Cancel / Apply. Apply sets <see cref="Applied"/> and closes; Cancel, Escape and the close box leave
/// every conflict pending.
/// </summary>
public sealed class SyncConflictWindow : DialogWindow
{
    public SyncConflictWindow(SyncConflictsViewModel viewModel)
        : base(DialogSize.Conflicts)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Title = "Sync conflicts";
        CanResize = true;

        List = new SyncConflictList { Entries = viewModel.Entries };
        List.ChoiceChanged += (_, e) => viewModel.Choose(e.Index, e.Choice);
        // The help is an (i) after the summary, its text in the tooltip (App README, help rule).
        var shell = new DialogShell { Heading = viewModel.Summary, Help = viewModel.Help, Content = List, ConfirmLabel = "Apply" };
        shell.Confirmed += (_, _) => Apply();
        shell.Cancelled += (_, _) => Close();
        Content = shell;
    }

    public SyncConflictList List { get; }

    /// <summary>True once Apply was pressed.</summary>
    public bool Applied { get; private set; }

    public void Apply()
    {
        Applied = true;
        Close();
    }
}
