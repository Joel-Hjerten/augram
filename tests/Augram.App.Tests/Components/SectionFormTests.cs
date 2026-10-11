using Augram.App.Components.Fields.Color;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.Inspector;
using Augram.App.Tests.Support;
using Augram.Core.Config;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

public sealed class SectionFormTests
{
    [AvaloniaFact]
    public void RendersOneEditorPerFieldKind()
    {
        var (form, _) = Show(new FakeOptions());

        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();

        Assert.Equal(8, rows.Count);
        Assert.IsType<CheckBox>(rows[0].Editor);
        Assert.IsType<ComboBox>(rows[1].Editor);
        Assert.IsType<WrapPanel>(rows[2].Editor);
        Assert.IsType<TextBox>(rows[3].Editor);
        Assert.IsType<NumericUpDown>(rows[4].Editor);
        Assert.IsType<ColorEditor>(rows[5].Editor);
        Assert.IsType<TextBlock>(rows[6].Editor);
        Assert.IsType<Button>(rows[7].Editor);
    }

    /// <summary>Help is an (i) after the label and the section title, its text in the tooltip, never a line under it (Joel, 2026-10-09).</summary>
    [AvaloniaFact]
    public void HelpIsAnInfoMarkWithItsTextInTheTooltip()
    {
        var screen = new FormScreen("Help", [new Section("Section", [new TextField("With help", new DelegateBinding<string>(() => "x"), "What it does."), new TextField("Without", new DelegateBinding<string>(() => "y"))], "About the section.")]);
        var form = new SectionForm { Screen = screen };
        var window = new Window { Width = 600, Height = 400, Content = form };
        window.Show();

        var marks = form.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("help-icon")).ToList();
        Assert.Equal(["About the section.", "What it does.", null], marks.Select(mark => mark.IsVisible ? (string?)ToolTip.GetTip(mark) : null));
        Assert.All(marks, mark => Assert.Equal("ⓘ", mark.Text));
        Assert.All(marks, mark => Assert.Equal(PlacementMode.Right, ToolTip.GetPlacement(mark)));
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<TextBlock>(), text => text.Text is "What it does." or "About the section.");
    }

    [AvaloniaFact]
    public void ViewModelChangesReachTheEditors()
    {
        var vm = new FakeOptions();
        var (form, _) = Show(vm);
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();

        vm.Flag = false;
        vm.Choice = "Gamma";
        vm.Name = "renamed";
        vm.Amount = 77;
        vm.Colour = new RgbColor(1, 2, 3);

        Assert.False(((CheckBox)rows[0].Editor!).IsChecked);
        Assert.Equal(2, ((ComboBox)rows[1].Editor!).SelectedIndex);
        Assert.Equal("renamed", ((TextBox)rows[3].Editor!).Text);
        Assert.Equal(77m, ((NumericUpDown)rows[4].Editor!).Value);
        Assert.Equal((1, 2, 3), (((ColorEditor)rows[5].Editor!).Red, ((ColorEditor)rows[5].Editor!).Green, ((ColorEditor)rows[5].Editor!).Blue));
        Assert.Contains("renamed", ((TextBlock)rows[6].Editor!).Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void EditorChangesReachTheViewModel()
    {
        var vm = new FakeOptions();
        var (form, _) = Show(vm);
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();

        ((CheckBox)rows[0].Editor!).IsChecked = false;
        ((ComboBox)rows[1].Editor!).SelectedIndex = 0;
        ((WrapPanel)rows[2].Editor!).Children.OfType<RadioButton>().First().IsChecked = true;
        ((TextBox)rows[3].Editor!).Text = "typed";
        ((NumericUpDown)rows[4].Editor!).Value = 42;
        ((ColorEditor)rows[5].Editor!).Red = 200;

        Assert.False(vm.Flag);
        Assert.Equal("Alpha", vm.Choice);
        Assert.Equal("Left", vm.Button);
        Assert.Equal("typed", vm.Name);
        Assert.Equal(42, vm.Amount);
        Assert.Equal(new RgbColor(200, 255, 64), vm.Colour);
    }

    [AvaloniaFact]
    public void AColourFromOutsideIsOneChange_NeverAHalfUpdatedOne()
    {
        var vm = new FakeOptions();
        var seen = new List<RgbColor>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FakeOptions.Colour))
            {
                seen.Add(vm.Colour);
            }
        };
        var (form, _) = Show(vm);
        var editor = (ColorEditor)form.GetVisualDescendants().OfType<FieldRow>().ToList()[5].Editor!;

        vm.Colour = new RgbColor(1, 2, 3);

        // Showing the form writes nothing back; the outside change is the only one, and the editor follows it whole.
        Assert.Equal([new RgbColor(1, 2, 3)], seen);
        Assert.Equal((1, 2, 3), (editor.Red, editor.Green, editor.Blue));
    }

    [AvaloniaFact]
    public void TheSwatchIsAPicker_WhoseColourIsWrittenAsOneChange()
    {
        var vm = new FakeOptions();
        var (form, _) = Show(vm);
        var editor = (ColorEditor)form.GetVisualDescendants().OfType<FieldRow>().ToList()[5].Editor!;
        var picker = editor.GetVisualDescendants().OfType<ColorPicker>().Single(found => found.Name == "PART_Picker");
        var changes = 0;
        vm.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(FakeOptions.Colour) ? 1 : 0;

        Assert.Same(picker, editor.Picker);
        Assert.Equal(Avalonia.Media.Color.FromRgb(0, 255, 64), editor.Picker!.Color);
        Assert.False(editor.Picker.IsAlphaEnabled);

        editor.Picker.Color = Avalonia.Media.Color.FromRgb(10, 20, 30);

        Assert.Equal(new RgbColor(10, 20, 30), vm.Colour);
        Assert.Equal(1, changes);
    }

    [AvaloniaFact]
    public void MarksRegionsWithDeclarationPathSourceAndBinding()
    {
        var (form, _) = Show(new FakeOptions());
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();

        var info = Region.GetInfo(rows[0]);

        Assert.Equal("Toggle", Region.GetName(rows[0]));
        Assert.NotNull(info);
        Assert.Equal("Test › Kinds › Toggle", info.Path);
        Assert.Equal("Toggle", info.Kind);
        Assert.Equal("Flag", info.BoundProperty);
        Assert.EndsWith("SectionFormTests.cs", info.Source.File, StringComparison.Ordinal);
        Assert.True(info.Source.Line > 0);
        Assert.Equal("Test › Kinds", Region.GetInfo(form.GetVisualDescendants().OfType<SectionView>().Single())?.Path);
    }

    [AvaloniaFact]
    public void ARowWithAVisibleBindingShowsOnlyWhileItReadsTrue()
    {
        var vm = new FakeOptions { Flag = false };
        var screen = new FormScreen("Test",
        [
            new Section("Kinds",
            [
                new NoteField("Always", "here"),
                new NoteField("Sometimes", "now you see me") { Visible = new DelegateBinding<bool>(() => vm.Flag, owner: vm) },
            ]),
        ]);
        var form = new SectionForm { Screen = screen };
        new Window { Content = form }.Show();
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();
        var always = rows.Single(row => row.Label == "Always");
        var sometimes = rows.Single(row => row.Label == "Sometimes");

        Assert.True(always.IsVisible);
        Assert.False(sometimes.IsVisible);

        vm.Flag = true;
        Assert.True(sometimes.IsVisible);
        vm.Flag = false;
        Assert.False(sometimes.IsVisible);
    }

    private static (SectionForm Form, Window Window) Show(FakeOptions vm)
    {
        var screen = new FormScreen("Test",
        [
            new Section("Kinds",
            [
                new ToggleField("Toggle", new DelegateBinding<bool>(() => vm.Flag, v => vm.Flag = v, vm)),
                new DropdownField<string>("Dropdown", Choice.FromStrings("Alpha", "Beta", "Gamma"), new DelegateBinding<string>(() => vm.Choice, v => vm.Choice = v, vm)),
                new ButtonRadioField<string>("Radio", Choice.FromStrings("Left", "Middle", "Right"), new DelegateBinding<string>(() => vm.Button, v => vm.Button = v, vm)),
                new TextField("Text", new DelegateBinding<string>(() => vm.Name, v => vm.Name = v, vm)),
                new NumberField("Number", new DelegateBinding<double>(() => vm.Amount, v => vm.Amount = v, vm), 0, 100),
                new ColorField("Color", new DelegateBinding<RgbColor>(() => vm.Colour, v => vm.Colour = v, vm)),
                new NoteField("Note", new DelegateBinding<string>(() => "Name is " + vm.Name, owner: vm)),
                new CustomField("Custom", () => new Button { Content = "custom" }),
            ]),
        ]);
        var form = new SectionForm { Screen = screen };
        var window = new Window { Content = form };
        window.Show();
        return (form, window);
    }
}
