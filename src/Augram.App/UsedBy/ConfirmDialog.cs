using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Augram.App.UsedBy;

/// <summary>
/// A yes/no question as a small modal dialog built from themed pieces (a <c>dialog</c> panel, a
/// wrapped message, <c>toolbar</c> buttons): the confirm button answers true, Cancel, Escape and the
/// close box answer false. Shown through <see cref="Window.ShowDialog{TResult}"/> by <see cref="ConfirmPresenter"/>,
/// or on its own with <see cref="ShowAloneAsync"/> when there is no main window to own it (a second launch's take-over
/// question, <c>Views/TakeOverPresenter</c>). With no cancel label it is a message with one button (OK).
/// </summary>
public sealed class ConfirmDialog : Window
{
    private readonly TextBlock _text;

    public ConfirmDialog(string title, string message, string confirmLabel, string? cancelLabel = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(confirmLabel);

        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MinHeight = 140;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        _text.Classes.Add("note");
        var buttons = new StackPanel();
        buttons.Classes.Add("dialog-buttons");
        if (cancelLabel is not null)
        {
            var cancel = new Button { Content = cancelLabel, IsCancel = true };
            cancel.Classes.Add("toolbar");
            cancel.Click += (_, _) => Answer(false);
            buttons.Children.Add(cancel);
        }

        var confirm = new Button { Content = confirmLabel, IsDefault = true };
        confirm.Classes.Add("toolbar");
        confirm.Click += (_, _) => Answer(true);
        buttons.Children.Add(confirm);

        var panel = new DockPanel();
        panel.Classes.Add("dialog");
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(_text);
        Content = panel;
    }

    /// <summary>The message on show; tests read it back.</summary>
    public string Message => _text.Text ?? string.Empty;

    /// <summary>True once the confirm button was pressed; the answer <see cref="ShowAloneAsync"/> returns.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>Shows the dialog as a window of its own, centred on the screen and activated; completes with <see cref="Confirmed"/> when it closes.</summary>
    public Task<bool> ShowAloneAsync()
    {
        var closed = new TaskCompletionSource<bool>();
        Closed += (_, _) => closed.TrySetResult(Confirmed);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Show();
        Activate();
        return closed.Task;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Answer(false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Answer(bool confirmed)
    {
        Confirmed = confirmed;
        Close(confirmed);
    }
}
