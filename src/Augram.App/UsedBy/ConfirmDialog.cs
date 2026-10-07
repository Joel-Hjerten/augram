using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Augram.App.UsedBy;

/// <summary>
/// A yes/no question as a small modal dialog built from themed pieces (a <c>dialog</c> panel, a
/// wrapped message, <c>toolbar</c> buttons): the confirm button answers true, Cancel, Escape and the
/// close box answer false. Shown through <see cref="Window.ShowDialog{TResult}"/> by <see cref="ConfirmPresenter"/>.
/// </summary>
public sealed class ConfirmDialog : Window
{
    private readonly TextBlock _text;

    public ConfirmDialog(string title, string message, string confirmLabel)
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
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Classes.Add("toolbar");
        cancel.Click += (_, _) => Close(false);
        var confirm = new Button { Content = confirmLabel, IsDefault = true };
        confirm.Classes.Add("toolbar");
        confirm.Click += (_, _) => Close(true);
        var buttons = new StackPanel();
        buttons.Classes.Add("dialog-buttons");
        buttons.Children.Add(cancel);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
