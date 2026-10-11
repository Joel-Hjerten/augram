using Augram.App.Components.FormDialog;
using Avalonia.Controls;

namespace Augram.App.Dialogs;

/// <summary>The window around a <see cref="FormDialog"/>: the form dialog's width (or the request's own), sized to its content's height, centred on the owner. Code-only; its one job is to host the component.</summary>
public sealed class FormDialogWindow : DialogWindow
{
    public FormDialogWindow(FormDialog dialog, string title, double? width = null)
        : base(DialogSize.Form)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        Title = title;
        if (width is { } own)
        {
            Width = own;
        }

        SizeToContent = SizeToContent.Height;
        CanResize = false;
        Content = dialog;
    }
}
