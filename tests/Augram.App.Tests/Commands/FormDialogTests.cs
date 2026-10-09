using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Components.WindowFinder;
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
        var edit = new GroupEditViewModel { Name = "Chrome" };
        edit.Identification.WindowsNames = "chrome.exe";
        var dialog = new FormDialog { Screen = edit.Declare(), ConfirmLabel = "Save" };
        var window = new Window { Content = dialog };
        window.Show();

        var rows = dialog.GetVisualDescendants().OfType<FieldRow>().ToList();

        Assert.True(dialog.HasScreen);
        Assert.False(dialog.HasMessage);
        Assert.Equal(
            ["Name", "Active", "Use on", "Suppress global commands", "Identify window", "Windows executables", "macOS executables", "Guess for an empty list",
                "Its path", "Its title", "Its classes",
                "Executable path", "Path is a regular expression", "Window title", "Title is a regular expression", "Window classes", "Not when full screen"],
            rows.Select(row => row.Label));
        // The app group gets the window finder through the identification form it shares with ignored apps.
        Assert.Contains(rows, row => row.Label == "Identify window" && row.Accessory is WindowFinder);
        Assert.False(rows.Single(row => row.Label == "Its path").IsVisible);
        ((TextBox)rows[0].Editor!).Text = "Chromium";
        ((CheckBox)rows[3].Editor!).IsChecked = true;
        var useOn = ((StackPanel)rows[2].Editor!).Children.OfType<CheckBox>().ToList();
        Assert.Equal(["Windows", "macOS"], useOn.Select(box => (string)box.Content!));
        useOn[1].IsChecked = false;
        Assert.False(edit.UseOnMac);
        Assert.True(edit.UseOnWindows);
        Assert.Equal("Chromium", edit.Name);
        Assert.True(edit.SuppressGlobals);
    }
}
