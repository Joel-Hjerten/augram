using System.ComponentModel;
using Augram.App.Components.SectionForm;
using Augram.App.Components.SyncConflictList;
using Augram.App.Dialogs;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Transfer;

/// <summary>
/// The import review's content (plan 0003 step 4) in the <see cref="DialogShell"/>, like the sync conflict dialog: the declared
/// top (<see cref="AugramImportScreen"/> in a <see cref="SectionForm"/>), the differing items in the shared
/// <see cref="SyncConflictList"/> ("Yours" beside "In the file", a choice per row; shown only when some differ), and Cancel /
/// Import, Import enabled while there is something to import. <see cref="AugramImportWindow"/> hosts it; the gallery shows it
/// as it is. Every intent goes to the view model.
/// </summary>
public sealed class AugramImportView : DialogShell
{
    private readonly AugramImportViewModel _viewModel;

    public AugramImportView(AugramImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        Form = new SectionForm { Screen = AugramImportScreen.Declare(viewModel) };
        List = new SyncConflictList
        {
            Entries = viewModel.ConflictEntries,
            MineHeader = AugramImportViewModel.MineHeader,
            TheirsHeader = AugramImportViewModel.TheirsHeader,
            IsVisible = viewModel.HasConflicts,
        };
        List.ChoiceChanged += (_, e) => viewModel.Choose(e.Index, e.Choice);

        // No margin round the review, as before the shell: it was a DockPanel subclass, which the theme's DockPanel.dialog
        // style (the margin) never matched. Kept as it looks; whether it should get the dialogs' margin is Joel's call.
        Margin = default;
        ConfirmLabel = AugramImportViewModel.ImportLabel;
        CanConfirm = viewModel.CanImport;
        Cancelled += (_, _) => viewModel.Cancel();
        Confirmed += (_, _) => viewModel.Import();

        var body = new DockPanel();
        DockPanel.SetDock(Form, Dock.Top);
        body.Children.Add(Form);
        body.Children.Add(List);
        Content = body;
        AttachedToVisualTree += (_, _) =>
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
            Follow();
        };
        DetachedFromVisualTree += (_, _) => _viewModel.PropertyChanged -= OnViewModelChanged;
    }

    public AugramImportViewModel ViewModel => _viewModel;

    public SectionForm Form { get; }

    public SyncConflictList List { get; }

    /// <summary>The Import button, once the shell's template is applied.</summary>
    public Button ImportButton => ConfirmButton ?? throw new InvalidOperationException("The review is not shown yet.");

    /// <summary>Styled as the <see cref="DialogShell"/> it is.</summary>
    protected override Type StyleKeyOverride => typeof(DialogShell);

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Follow();

    /// <summary>The list shows the view model's rows (new ones after "Apply to all"), and Import is enabled while it can import.</summary>
    private void Follow()
    {
        if (!ReferenceEquals(List.Entries, _viewModel.ConflictEntries))
        {
            List.Entries = _viewModel.ConflictEntries;
        }

        CanConfirm = _viewModel.CanImport;
    }
}
