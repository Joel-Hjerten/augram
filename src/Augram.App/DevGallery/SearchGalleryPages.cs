#if DEBUG
using Augram.App.Components.GesturePicker;
using Augram.App.Components.SearchBox;
using Augram.App.Declarations;
using Augram.App.Dialogs;
using Augram.Core.Gestures;

namespace Augram.App.DevGallery;

/// <summary>
/// Gallery pages for the search on the lists (plan 0009): the <see cref="SearchBox"/> in its states, and each list with a
/// search active (the Commands Apps workbench, the gesture grid, the Exclusions list and the gesture picker over the areas'
/// own fakes), the empty state included.
/// </summary>
public static class SearchGalleryPages
{
    public static ScreenDeclaration SearchBoxPage() =>
        new FormScreen("SearchBox",
        [
            new Section("States",
            [
                new CustomField("Empty", () => new SearchBox()),
                new CustomField("With text", () => new SearchBox { Text = "down left" }),
                new CustomField("Own placeholder", () => new SearchBox { Placeholder = "Search gestures" }),
                new CustomField("Focused", () =>
                {
                    var box = new SearchBox { Text = "close" };
                    box.Loaded += (_, _) => box.Focus();
                    return box;
                }),
            ]),
        ]);

    /// <summary>The ItemList page's list with "Row 1" typed: the rows whose message has both words, after the dropdown filter.</summary>
    public static ScreenDeclaration ItemListSearchPage()
    {
        var screen = (ListScreen)GalleryFakes.ListPage();
        var text = "row 1";
        return screen with
        {
            Title = "ItemList, searching",
            Spec = screen.Spec with
            {
                Title = "ItemList with \"row 1\" in the search box",
                Search = new ListSearch(new DelegateBinding<string>(() => text, v => text = v), row => ((GalleryFakes.FakeRow)row).Message, "rows"),
            },
        };
    }

    /// <summary>Commands › Apps with "zoom" typed: Chrome and Blender narrowed to their Zoom commands, expanded; Zoom in selected.</summary>
    public static ScreenDeclaration AppsWorkbenchSearchPage() => CommandGalleryPages.AppsWorkbenchPage(search: "zoom");

    /// <summary>Commands › Global with a search that matches nothing: the empty state.</summary>
    public static ScreenDeclaration GlobalWorkbenchNoMatchPage() => CommandGalleryPages.GlobalWorkbenchPage(search: "xyz");

    /// <summary>The Gestures tab with "down" typed: the tiles whose name has it.</summary>
    public static ScreenDeclaration GridSearchPage() => GestureGalleryPages.GridPage(search: "down");

    /// <summary>Exclusions › Global with "blend" typed: Blender by name.</summary>
    public static ScreenDeclaration IgnoredSearchPage() => IgnoredGalleryPages.IgnoredPage(search: "blend");

    /// <summary>The picker with "do" typed: Down and the gestures starting with it first, then the rest with it in the name.</summary>
    public static ScreenDeclaration GesturePickerSearchPage() =>
        new FormScreen("GesturePicker, searching",
        [
            new Section("The Select Gesture popup's content with \"do\" in the search box, Down selected",
            [
                new CustomField("GesturePicker", () =>
                {
                    var picker = new GesturePicker { Tiles = GesturePickerPresenter.Tiles(StarterGestures.All(), SearchQuery.Parse("do")), SearchText = "do", Width = DialogSize.Width(DialogSize.Picker), Height = 420 };
                    picker.Select(StarterGestures.IdFor("Down"));
                    return picker;
                }),
                new CustomField("Nothing matches", () => new GesturePicker { Tiles = [], SearchText = "xyz", Width = DialogSize.Width(DialogSize.Picker), Height = 240 }),
            ]),
        ]);
}
#endif
