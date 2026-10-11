using Avalonia.Controls;
using Avalonia.Media;

namespace Augram.App.Dialogs;

/// <summary>
/// A yes/no question as a small dialog: a wrapped message in the <see cref="DialogShell"/>, the confirm button answers
/// true, Cancel, Escape and the close box answer false. <see cref="ConfirmPresenter"/> shows it, modal to the main window
/// or, with <see cref="ShowAloneAsync"/>, as a window of its own (a second launch's take-over question). With no cancel
/// label it is a message with one button (OK).
/// </summary>
public sealed class ConfirmDialog : DialogWindow
{
    private readonly TextBlock _text;

    public ConfirmDialog(string title, string message, string confirmLabel, string? cancelLabel = "Cancel")
        : base(DialogSize.Confirm)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(confirmLabel);

        Title = title;
        SizeToContent = SizeToContent.Height;
        CanResize = false;

        _text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        _text.Classes.Add("note");
        var shell = new DialogShell { Content = _text, ConfirmLabel = confirmLabel, CancelLabel = cancelLabel };
        shell.Confirmed += (_, _) => Answer(true);
        shell.Cancelled += (_, _) => Answer(false);
        Content = shell;
    }

    /// <summary>The message on show; tests read it back.</summary>
    public string Message => _text.Text ?? string.Empty;

    /// <summary>True once the confirm button was pressed; the answer <see cref="ShowAloneAsync"/> returns.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>Shows the dialog as a window of its own (<see cref="DialogHost.ShowAloneAsync"/>); completes with <see cref="Confirmed"/> when it closes.</summary>
    public Task<bool> ShowAloneAsync() =>
        DialogHost.ShowAloneAsync(this).ContinueWith(_ => Confirmed, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private void Answer(bool confirmed)
    {
        Confirmed = confirmed;
        Close(confirmed);
    }
}
