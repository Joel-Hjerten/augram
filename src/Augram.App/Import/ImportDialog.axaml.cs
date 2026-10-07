using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Import;

public sealed partial class ImportDialog : Window
{
    /// <summary>For the XAML previewer and runtime loader only.</summary>
    public ImportDialog()
        : this(new ImportViewModel(new Core.Gestures.GestureLibrary(), new Core.Mapping.MappingStore(), Core.Diagnostics.NullEventLog.Instance))
    {
    }

    public ImportDialog(ImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Finished += (_, _) => Close();
    }
}
