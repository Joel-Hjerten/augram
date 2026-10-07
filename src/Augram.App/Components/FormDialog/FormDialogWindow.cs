using Avalonia.Controls;

namespace Augram.App.Components.FormDialog;

/// <summary>The window around a <see cref="FormDialog"/>: sized to its content, centred on the owner. Code-only; its one job is to host the component.</summary>
public sealed class FormDialogWindow : Window
{
    public FormDialogWindow(FormDialog dialog, string title)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        Title = title;
        Width = 560;
        MinWidth = 360;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = dialog;
    }
}
