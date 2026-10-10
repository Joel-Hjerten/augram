#if DEBUG
using Augram.App.Components.MasterDetail;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.App.ViewModels.Ignored;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>The gallery's Ignored per command page (gallery rule, plan 0004 step 7): Spine selected, its form with "Used by" as a link, the move entry labelled.</summary>
public sealed class IgnoredGalleryTests
{
    [AvaloniaFact]
    public void ThePerCommandPageShowsSpineWithItsUsedBy()
    {
        var view = Assert.IsType<MasterDetail>(Assert.IsType<ComponentScreen>(IgnoredGalleryPages.PerCommandPage()).Build());
        new Window { Content = view, Width = 1200, Height = 1400 }.Show();

        Assert.Equal(["Eyeris", "Krita", "Spine"], view.Items.Select(item => item.Name));
        Assert.Equal("Spine", view.SelectedItem!.Name);
        Assert.Equal("Move to Global", view.MoveLabel);
        var row = view.GetVisualDescendants().OfType<FieldRow>().Single(candidate => candidate.Label == IgnoredEditViewModel.UsedByLabel);
        var link = Assert.Single(row.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("link"));
        Assert.Equal("Global › Media › Zoom in", link.Content);
    }

    /// <summary>Plan 0005: Blender on Exclusions › Global shows "Allowed for: Magnifier" (Joel's done-when).</summary>
    [AvaloniaFact]
    public void TheAllowedForPageShowsBlenderAllowedForTheMagnifier()
    {
        var view = Assert.IsType<MasterDetail>(Assert.IsType<ComponentScreen>(IgnoredGalleryPages.AllowedForPage()).Build());
        new Window { Content = view, Width = 1200, Height = 1400 }.Show();

        Assert.Equal("Blender", view.SelectedItem!.Name);
        Assert.DoesNotContain(view.Items, item => item.Name is "Spine" or "Eyeris");
        var row = view.GetVisualDescendants().OfType<FieldRow>().Single(candidate => candidate.Label == IgnoredEditViewModel.AllowedForLabel);
        var link = Assert.Single(row.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("link"));
        Assert.Equal("Global › Media › Magnifier", link.Content);
    }
}
#endif
