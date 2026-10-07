#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Components.SyncConflictList;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>The gallery pages of the two sync dialogs build and render over their fake data (gallery rule).</summary>
public sealed class SyncGalleryTests
{
    [AvaloniaFact]
    public void TheJoinPageShowsTheDialogForTwoMachinesAndForOne()
    {
        var form = Render(SyncGalleryPages.JoinPage());

        var dialogs = form.GetVisualDescendants().OfType<FormDialog>().ToList();
        Assert.Equal(2, dialogs.Count);
        Assert.All(dialogs, dialog => Assert.Equal("Join", dialog.ConfirmLabel));
        Assert.Equal(["PC-HOME", "Mac"], dialogs[0].Screen!.Sections[0].Fields.Select(field => field.Label));
    }

    [AvaloniaFact]
    public void TheConflictsPageShowsEveryKindAndAnEmptyList()
    {
        var form = Render(SyncGalleryPages.ConflictsPage());

        var lists = form.GetVisualDescendants().OfType<SyncConflictList>().ToList();
        Assert.Equal(2, lists.Count);
        var rows = lists[0].GetVisualDescendants().OfType<SyncConflictRow>().ToList();
        Assert.Equal(5, rows.Count);
        Assert.True(rows[0].HasMineGlyph && rows[0].HasTheirsGlyph);
        Assert.Empty(lists[1].Rows);
    }

    private static SectionForm Render(ScreenDeclaration page)
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(page) };
        new Window { Content = form, Width = 1200, Height = 1200 }.Show();
        return form;
    }
}
#endif
