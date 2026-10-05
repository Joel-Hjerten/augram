using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Import.StrokesPlus;
using Avalonia.Controls;

namespace Augram.App.Import;

/// <summary>Content of the import dialog; the only code is the "apply to all" button forwarding its combo's choice.</summary>
public sealed partial class ImportView : UserControl
{
    public ImportView()
    {
        InitializeComponent();
        AllChoices.SelectedIndex = 0;
        ApplyToAll.Click += (_, _) =>
        {
            if (DataContext is ImportViewModel viewModel && AllChoices.SelectedItem is Choice<MergeChoice> choice)
            {
                viewModel.ApplyToAll(choice.Value);
            }
        };
    }
}
