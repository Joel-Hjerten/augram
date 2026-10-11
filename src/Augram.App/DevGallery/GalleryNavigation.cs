#if DEBUG
using Augram.App.Components.Fields.Color;
using Augram.App.Components.Shell;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.Core.Config;
using Avalonia.Controls;

namespace Augram.App.DevGallery;

/// <summary>
/// The dev gallery (F7, ADR-0002 §5): one sub-tab per shared component, each shown in its states
/// with fake data, in the dark and the light theme side by side (<see cref="ThemePair"/>, plan 0006 step 6). A component
/// is not done until it has an entry here. Debug builds only.
/// </summary>
public static class GalleryNavigation
{
    public const string Key = "gallery";

    public static NavEntry Entry() => new("Gallery", Key, SubEntries:
        [.. Pages.Select(page => page with { Screen = () => ThemePair.Declare(page.Title, page.Screen!) })]);

    /// <summary>The pages as declared, one theme's worth each (tests render them).</summary>
    internal static IReadOnlyList<NavEntry> Pages { get; } =
    [
        new("SectionForm", Key + ".sectionform", SectionFormPage),
        new("FieldRow", Key + ".fieldrow", FieldRowPage),
        new("ItemList", Key + ".itemlist", GalleryFakes.ListPage),
        new("Shell", Key + ".shell", ShellPage),
        new("ColorEditor", Key + ".coloreditor", ColorEditorPage),
        new("TextPanel", Key + ".textpanel", () => new TextScreen("TextPanel", "A TextPanel: wrapped text with theme padding. Placeholder tabs use it.")),
        new("GestureGlyph", Key + ".gestureglyph", GestureGalleryPages.GlyphPage),
        new("GestureGrid", Key + ".gesturegrid", GestureGalleryPages.GridPage),
        new("GestureDrawArea", Key + ".gesturedrawarea", GestureGalleryPages.DrawAreaPage),
        new("Shape cleanup", Key + ".shapecleanup", ShapeCleanupGalleryPage.Page),
        new("Training", Key + ".training", GestureGalleryPages.TrainingPage),
        new("Import", Key + ".import", GestureGalleryPages.ImportPage),
        new("Commands Global", Key + ".commands.global", CommandGalleryPages.GlobalWorkbenchPage),
        new("Commands Apps", Key + ".commands.apps", CommandGalleryPages.AppsWorkbenchPage),
        new("Hold remaps", Key + ".holdremaps", CommandGalleryPages.HoldRemapsPage),
        new("Drag distance, Not in", Key + ".dragdistance", CommandGalleryPages.DragDistanceNotInPage),
        new("Trigger conflict", Key + ".triggerconflict", TriggerConflictGalleryPage.Page),
        new("Button trigger", Key + ".buttontrigger", ButtonTriggerGalleryPage.WorkbenchPage),
        new("Button trigger states", Key + ".buttontrigger.states", ButtonTriggerGalleryPage.Page),
        new("StepList", Key + ".steplist", CommandGalleryPages.StepListPage),
        new("StepForms", Key + ".stepforms", CommandGalleryPages.StepFormsPage),
        new("StepTypePicker", Key + ".steptypepicker", CommandGalleryPages.StepTypePickerPage),
        new("GesturePicker", Key + ".gesturepicker", CommandGalleryPages.GesturePickerPage),
        new("FormDialog", Key + ".formdialog", CommandGalleryPages.FormDialogPage),
        new("Exclusions", Key + ".ignored", IgnoredGalleryPages.IgnoredPage),
        new("Exclusions per command", Key + ".ignored.percommand", IgnoredGalleryPages.PerCommandPage),
        new("Exclusions allowed for", Key + ".ignored.allowedfor", IgnoredGalleryPages.AllowedForPage),
        new("MasterDetail", Key + ".masterdetail", IgnoredGalleryPages.MasterDetailPage),
        new("WindowFinder", Key + ".windowfinder", IgnoredGalleryPages.WindowFinderPage),
        new("Sync join", Key + ".syncjoin", SyncGalleryPages.JoinPage),
        new("Sync conflicts", Key + ".syncconflicts", SyncGalleryPages.ConflictsPage),
        new("Transfer", Key + ".transfer", TransferGalleryPages.Page),
    ];

    private static ScreenDeclaration SectionFormPage()
    {
        var fake = new GalleryFakes.FakeSettings();
        return new FormScreen("SectionForm: every field kind",
        [
            new Section("All kinds, bound to one fake view model",
            [
                new ToggleField("Toggle", new DelegateBinding<bool>(() => fake.Flag, v => fake.Flag = v, fake), "A bool."),
                new TogglesField("Toggles", [new ToggleOption("First", new DelegateBinding<bool>(() => fake.Flag, v => fake.Flag = v, fake)), new ToggleOption("Second", new DelegateBinding<bool>(() => !fake.Flag, v => fake.Flag = !v, fake))], "Several bools on one row."),
                new CheckListField("CheckList",
                [
                    new CheckListItem("Global", new DelegateBinding<bool>(() => fake.Flag, v => fake.Flag = v, fake), "7 commands"),
                    new CheckListItem("Blender", new DelegateBinding<bool>(() => !fake.Flag, v => fake.Flag = !v, fake), "5 commands · 1 hold remap · Windows only"),
                    new CheckListItem("No detail", new DelegateBinding<bool>(() => fake.Flag, v => fake.Flag = v, fake)),
                ], "Bools one under the other, each with an optional detail; scrolls past its height."),
                new DropdownField<string>("Dropdown", Choice.FromStrings("Alpha", "Beta", "Gamma"), new DelegateBinding<string>(() => fake.Choice, v => fake.Choice = v, fake)),
                new ButtonRadioField<string>("ButtonRadio", Choice.FromStrings("Left", "Middle", "Right"), new DelegateBinding<string>(() => fake.Button, v => fake.Button = v, fake)),
                new TextField("Text", new DelegateBinding<string>(() => fake.Name, v => fake.Name = v, fake)),
                new NumberField("Number", new DelegateBinding<double>(() => fake.Amount, v => fake.Amount = v, fake), 0, 100, 5, "0–100 in steps of 5."),
                new SliderField("Slider", new DelegateBinding<double>(() => fake.Amount, v => fake.Amount = v, fake), 0, 100, 1, "%", "The Number above on a slider, 0–100 in steps of 1."),
                new SliderField("Slider, enabled by Toggle", new DelegateBinding<double>(() => fake.Amount / 5, v => fake.Amount = v * 5, fake), 0, 20, 1, " px", "Disabled while the Toggle above is off (Field.Enabled).")
                {
                    Enabled = new DelegateBinding<bool>(() => fake.Flag, owner: fake),
                },
                new ColorField("Color", new DelegateBinding<RgbColor>(() => fake.Colour, v => fake.Colour = v, fake)),
                new ColorField("Color with presets", new DelegateBinding<RgbColor>(() => fake.Colour, v => fake.Colour = v, fake), "The rainbow swatches; the Color above picks the same colour.")
                {
                    Presets = ColourPresets.Rainbow,
                },
                new ColorField("Presets, custom colour", new DelegateBinding<RgbColor>(() => fake.CustomColour, v => fake.CustomColour = v, fake), "A colour that is no preset: one more swatch at the end, ringed.")
                {
                    Presets = ColourPresets.Rainbow,
                },
                new NoteField("Note", new DelegateBinding<string>(() => $"Live: {fake.Name} / {fake.Amount} / {fake.Colour}", owner: fake), "Updates as the others change."),
                new LinksField("Links", new DelegateBinding<IReadOnlyList<LinkItem>>(() => fake.Flag ? [new("Global › Media › Zoom in", () => fake.Flag = false), new("Chrome › Zoom in", () => { }, "inactive"), new("Plain text, no action", null)] : [], owner: fake, propertyName: null), "none", "Links one under the other; the first clears the Toggle above, which empties the list."),
                new CustomField("Custom", () => new Button { Content = "A custom control" }),
            ]),
            new Section("Second section, with help", [new NoteField("Only note", "Sections stack vertically.")], "Section help line."),
        ]);
    }

    private static ScreenDeclaration FieldRowPage()
    {
        var fake = new GalleryFakes.FakeSettings();
        return new FormScreen("FieldRow states",
        [
            new Section("States",
            [
                new ToggleField("With help", new DelegateBinding<bool>(() => fake.Flag, v => fake.Flag = v, fake), "Help text under the label."),
                new ToggleField("Without help", new DelegateBinding<bool>(() => fake.Flag, v => fake.Flag = v, fake)),
                new ToggleField("Read-only binding", new DelegateBinding<bool>(() => fake.Flag, owner: fake), "Setter omitted: editor disabled."),
                new TextField("Long label that will need to wrap inside the label column of the row", new DelegateBinding<string>(() => fake.Name, v => fake.Name = v, fake)),
            ]),
        ]);
    }

    private static ScreenDeclaration ShellPage() =>
        new FormScreen("Shell",
        [
            new Section("Nested shell with two tabs, one of them with sub-tabs",
            [
                new CustomField("Shell", () => new Shell { Registry = GalleryFakes.NestedRegistry(), Height = 260 }),
            ]),
        ]);

    private static ScreenDeclaration ColorEditorPage() =>
        new FormScreen("ColorEditor",
        [
            new Section("States",
            [
                new CustomField("Green (trail default)", () => new ColorEditor { Red = 0, Green = 255, Blue = 64 }),
                new CustomField("Black", () => new ColorEditor()),
                new CustomField("Disabled", () => new ColorEditor { Red = 200, Green = 30, Blue = 30, IsEnabled = false }),
            ]),
        ]);
}
#endif
