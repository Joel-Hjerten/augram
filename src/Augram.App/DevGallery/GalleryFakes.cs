#if DEBUG
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.DevGallery;

/// <summary>Fake data for the gallery: a settings-like view model, list rows, and a nested registry.</summary>
public static partial class GalleryFakes
{
    public static NavigationRegistry NestedRegistry() => new(
    [
        new NavEntry("First", "nested.first", () => new TextScreen("First", "First nested tab.")),
        new NavEntry("Second", "nested.second", SubEntries:
        [
            new NavEntry("Sub A", "nested.second.a", () => new TextScreen("Sub A", "Sub-tab A.")),
            new NavEntry("Sub B", "nested.second.b", () => new TextScreen("Sub B", "Sub-tab B.")),
        ]),
    ]);

    public static ScreenDeclaration ListPage()
    {
        var rows = Enumerable.Range(1, 40).Select(i => new FakeRow(i, i % 3 == 0 ? "warn" : "info", $"Row {i} says hello")).ToList();
        var source = new ListSource<FakeRow>(() => rows);
        var filter = "All";
        return new ListScreen("ItemList", new ListSpec(
            "ItemList with filter, toolbar, context menu and keymap",
            Columns:
            [
                new ListColumn("#", row => ((FakeRow)row).Number.ToString(System.Globalization.CultureInfo.InvariantCulture), 50),
                new ListColumn("Level", row => ((FakeRow)row).Level, 70),
                new ListColumn("Message", row => ((FakeRow)row).Message),
            ],
            Source: source,
            Toolbar:
            [
                new ListAction("Add row", () =>
                {
                    rows.Add(new FakeRow(rows.Count + 1, "info", "Added by toolbar"));
                    source.NotifyChanged();
                }),
            ],
            Filters:
            [
                new ListFilter("Level", ["All", "info", "warn"], new DelegateBinding<string>(() => filter, v => filter = v),
                    (row, selected) => selected == "All" || ((FakeRow)row).Level == selected),
            ],
            ContextMenu: [new ListAction("Remove", row => { if (row is FakeRow r) { rows.Remove(r); source.NotifyChanged(); } })],
            Keymap: [new ListKey("Delete", new ListAction("Remove", row => { if (row is FakeRow r) { rows.Remove(r); source.NotifyChanged(); } }))],
            AutoScroll: true));
    }

    public sealed record FakeRow(int Number, string Level, string Message);

    public sealed partial class FakeSettings : ObservableObject
    {
        [ObservableProperty]
        public partial bool Flag { get; set; } = true;

        [ObservableProperty]
        public partial string Choice { get; set; } = "Beta";

        [ObservableProperty]
        public partial string Button { get; set; } = "Right";

        [ObservableProperty]
        public partial string Name { get; set; } = "Fake name";

        [ObservableProperty]
        public partial double Amount { get; set; } = 35;

        [ObservableProperty]
        public partial RgbColor Colour { get; set; } = new(0, 255, 64);
    }
}
#endif
