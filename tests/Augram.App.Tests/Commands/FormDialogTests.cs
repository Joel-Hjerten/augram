using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.ViewModels.Commands;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class FormDialogTests
{
    [AvaloniaFact]
    public void AMessageOnlyDialogShowsTheLineAndItsButtonsRaiseConfirmedOrCancelled()
    {
        var dialog = new FormDialog { Message = "Nothing to fill in here.", ConfirmLabel = "Delete" };
        var outcomes = new List<string>();
        dialog.Confirmed += (_, _) => outcomes.Add("confirmed");
        dialog.Cancelled += (_, _) => outcomes.Add("cancelled");
        var window = new Window { Content = dialog };
        window.Show();

        Assert.True(dialog.HasMessage);
        Assert.False(dialog.HasScreen);
        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == dialog.Message);
        var buttons = dialog.GetVisualDescendants().OfType<Button>().ToDictionary(button => (string)button.Content!);
        Assert.True(buttons["Delete"].IsDefault);
        Assert.True(buttons["Cancel"].IsCancel);

        buttons["Delete"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        buttons["Cancel"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["confirmed", "cancelled"], outcomes);
    }

    [AvaloniaFact]
    public void TheGroupFormRendersItsDeclaredFieldsAndWritesBackToTheEditState()
    {
        var edit = new GroupEditViewModel { Name = "Chrome", ProcessNames = "chrome.exe" };
        var dialog = new FormDialog { Screen = edit.Declare(), ConfirmLabel = "Save" };
        var window = new Window { Content = dialog };
        window.Show();

        var rows = dialog.GetVisualDescendants().OfType<FieldRow>().ToList();

        Assert.True(dialog.HasScreen);
        Assert.False(dialog.HasMessage);
        Assert.Equal(["Name", "Active", "Suppress global commands", "Executable names", "Window title", "Title is a regular expression", "Pick a window"], rows.Select(row => row.Label));
        ((TextBox)rows[0].Editor!).Text = "Chromium";
        ((CheckBox)rows[2].Editor!).IsChecked = true;
        Assert.Equal("Chromium", edit.Name);
        Assert.True(edit.SuppressGlobals);
    }
}
