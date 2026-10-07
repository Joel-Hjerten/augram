using Augram.App.Components.SyncConflictList;
using Augram.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Sync;

/// <summary>
/// The conflict dialog (F8 sync): a title line ("2 conflicts with Mac"), a help line, the
/// <see cref="SyncConflictList"/> over <see cref="SyncConflictsViewModel.Entries"/> (a choice per conflict, kept by
/// the view model) and Cancel / Apply, built from themed pieces like the "Used by…" popup. Apply sets
/// <see cref="Applied"/> and closes; Cancel, Escape and the close box leave every conflict pending.
/// </summary>
public sealed class SyncConflictWindow : Window
{
    public SyncConflictWindow(SyncConflictsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Title = "Sync conflicts";
        Width = 960;
        Height = 480;
        MinWidth = 640;
        MinHeight = 280;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        List = new SyncConflictList { Entries = viewModel.Entries };
        List.ChoiceChanged += (_, e) => viewModel.Choose(e.Index, e.Choice);
        var title = new TextBlock { Text = viewModel.Summary };
        title.Classes.Add("section-title");
        var help = new TextBlock { Text = SyncConflictsViewModel.Help };
        help.Classes.Add("help");
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Classes.Add("toolbar");
        cancel.Click += (_, _) => Close();
        var apply = new Button { Content = "Apply", IsDefault = true };
        apply.Classes.Add("toolbar");
        apply.Click += (_, _) => Apply();
        var buttons = new StackPanel();
        buttons.Classes.Add("dialog-buttons");
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);

        var panel = new DockPanel();
        panel.Classes.Add("dialog");
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(help, Dock.Top);
        panel.Children.Add(buttons);
        panel.Children.Add(title);
        panel.Children.Add(help);
        panel.Children.Add(List);
        Content = panel;
    }

    public SyncConflictList List { get; }

    /// <summary>True once Apply was pressed.</summary>
    public bool Applied { get; private set; }

    public void Apply()
    {
        Applied = true;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
