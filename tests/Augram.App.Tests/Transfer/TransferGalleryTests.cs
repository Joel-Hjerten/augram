#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Transfer;

/// <summary>The gallery's Transfer page builds and renders over its fake data (gallery rule): the export dialog in each scope.</summary>
public sealed class TransferGalleryTests
{
    [AvaloniaFact]
    public void TheExportDialogShowsEachScope_WithTheNoteWhereStepsTypeText_AndSaveDisabledWhenNothingIsSelected()
    {
        var form = Render(TransferGalleryPages.Page());

        var dialogs = form.GetVisualDescendants().OfType<FormDialog>().ToList();
        Assert.Equal(5, dialogs.Count);
        Assert.All(dialogs, dialog => Assert.Equal(ExportViewModel.ConfirmLabel, dialog.ConfirmLabel));
        Assert.Equal([true, false, false, true, false], dialogs.Select(NoteShown));
        Assert.Equal([true, true, true, true, false], dialogs.Select(dialog => dialog.CanConfirm));
        Assert.False(dialogs[4].GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, ExportViewModel.ConfirmLabel)).IsEnabled);
    }

    /// <summary>The changed copy shows every case the review has; the configuration's own export has nothing to import.</summary>
    [AvaloniaFact]
    public void TheImportReviewShowsAChangedCopy_AndAFileWithNothingToImport()
    {
        var form = Render(TransferGalleryPages.Page());

        var reviews = form.GetVisualDescendants().OfType<AugramImportView>().ToList();
        Assert.Equal(2, reviews.Count);
        var changed = reviews[0].ViewModel;
        Assert.NotEmpty(reviews[0].List.Entries);
        Assert.True(reviews[0].ImportButton.IsEnabled);
        Assert.Contains("New: ", changed.CountsText, StringComparison.Ordinal);
        Assert.Contains("Same as yours: ", changed.CountsText, StringComparison.Ordinal);
        Assert.Contains("Different: ", changed.CountsText, StringComparison.Ordinal);
        Assert.Contains("in the file is your 'Steam games'", changed.MatchesText, StringComparison.Ordinal);
        Assert.Contains("has the shape of your 'Up'", changed.MatchesText, StringComparison.Ordinal);
        Assert.True(changed.HasOutcome);
        Assert.True(changed.HasSettings);
        Assert.False(reviews[1].ImportButton.IsEnabled);
        Assert.False(reviews[1].List.IsVisible);
    }

    private static bool NoteShown(FormDialog dialog)
        => dialog.GetVisualDescendants().OfType<FieldRow>().Single(row => row.Label == "Typed text").IsVisible;

    private static SectionForm Render(ScreenDeclaration page)
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(page) };
        new Window { Content = form, Width = 1200, Height = 1200 }.Show();
        return form;
    }
}
#endif
