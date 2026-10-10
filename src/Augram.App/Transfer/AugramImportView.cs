using System.ComponentModel;
using Augram.App.Components.SectionForm;
using Augram.App.Components.SyncConflictList;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Transfer;

/// <summary>
/// The import review's content (plan 0003 step 4), built from themed pieces like the sync conflict dialog: the declared top
/// (<see cref="AugramImportScreen"/> in a <see cref="SectionForm"/>), the differing items in the shared
/// <see cref="SyncConflictList"/> ("Yours" beside "In the file", a choice per row; shown only when some differ), and Cancel /
/// Import, Import enabled while there is something to import. <see cref="AugramImportWindow"/> hosts it; the gallery shows it
/// as it is. Every intent goes to the view model.
/// </summary>
public sealed class AugramImportView : DockPanel
{
    private readonly AugramImportViewModel _viewModel;

    public AugramImportView(AugramImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Classes.Add("dialog");

        Form = new SectionForm { Screen = AugramImportScreen.Declare(viewModel) };
        List = new SyncConflictList
        {
            Entries = viewModel.ConflictEntries,
            MineHeader = AugramImportViewModel.MineHeader,
            TheirsHeader = AugramImportViewModel.TheirsHeader,
            IsVisible = viewModel.HasConflicts,
        };
        List.ChoiceChanged += (_, e) => viewModel.Choose(e.Index, e.Choice);

        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Classes.Add("toolbar");
        cancel.Click += (_, _) => viewModel.Cancel();
        ImportButton = new Button { Content = AugramImportViewModel.ImportLabel, IsDefault = true, IsEnabled = viewModel.CanImport };
        ImportButton.Classes.Add("toolbar");
        ImportButton.Click += (_, _) => viewModel.Import();
        var buttons = new StackPanel();
        buttons.Classes.Add("dialog-buttons");
        buttons.Children.Add(cancel);
        buttons.Children.Add(ImportButton);

        SetDock(buttons, Dock.Bottom);
        SetDock(Form, Dock.Top);
        Children.Add(buttons);
        Children.Add(Form);
        Children.Add(List);
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

    public Button ImportButton { get; }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Follow();

    /// <summary>The list shows the view model's rows (new ones after "Apply to all"), and Import is enabled while it can import.</summary>
    private void Follow()
    {
        if (!ReferenceEquals(List.Entries, _viewModel.ConflictEntries))
        {
            List.Entries = _viewModel.ConflictEntries;
        }

        ImportButton.IsEnabled = _viewModel.CanImport;
    }
}
