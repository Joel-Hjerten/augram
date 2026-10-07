using Augram.App.UsedBy;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}
