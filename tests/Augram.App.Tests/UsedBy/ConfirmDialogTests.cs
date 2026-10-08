using Augram.App.UsedBy;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.UsedBy;

public sealed class ConfirmDialogTests
{
    [AvaloniaFact]
    public void ShowsTheQuestionWithACancelAndTheConfirmButton()
    {
        var dialog = new ConfirmDialog("Delete gesture", "Delete 'Down'? It is used by Global › Minimize.", "Delete");
        dialog.Show();

        Assert.Equal("Delete gesture", dialog.Title);
        Assert.Equal("Delete 'Down'? It is used by Global › Minimize.", dialog.Message);
        var buttons = dialog.GetVisualDescendants().OfType<Button>().Select(button => (string?)button.Content).ToList();
        Assert.Equal(["Cancel", "Delete"], buttons);
        dialog.Close();
    }

    [AvaloniaFact]
    public void ShownAlone_ItAnswersWithTheButtonPressed()
    {
        // A second launch's take-over question: no main window exists to own it.
        var dialog = new ConfirmDialog("Augram (Dev)", "Augram 0.2.0 (installed) is already running.", "Quit it and start this one");
        var answer = dialog.ShowAloneAsync();
        Assert.True(dialog.IsVisible);
        Assert.False(answer.IsCompleted);

        Press(dialog, "Quit it and start this one");

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.True(answer.Result);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact]
    public void ShownAlone_CancelAnswersNo()
    {
        var dialog = new ConfirmDialog("Augram (Dev)", "Augram 0.2.0 (installed) is already running.", "Quit it and start this one");
        var answer = dialog.ShowAloneAsync();

        Press(dialog, "Cancel");

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.False(answer.Result);
    }

    [AvaloniaFact]
    public void WithoutACancelLabel_ItIsAMessageWithOneButton()
    {
        var dialog = new ConfirmDialog("Augram (Dev)", "It did not quit within 10 seconds.", "OK", cancelLabel: null);
        var closed = dialog.ShowAloneAsync();

        var buttons = dialog.GetVisualDescendants().OfType<Button>().Select(button => (string?)button.Content).ToList();
        Assert.Equal(["OK"], buttons);
        Press(dialog, "OK");
        Assert.True(closed.IsCompletedSuccessfully);
    }

    private static void Press(Window dialog, string label) =>
        dialog.GetVisualDescendants().OfType<Button>().Single(button => (string?)button.Content == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
