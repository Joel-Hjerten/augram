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
        Assert.IsType<StackPanel>(rows[2].Editor);
        Assert.IsType<TextBox>(rows[3].Editor);
        Assert.IsType<NumericUpDown>(rows[4].Editor);
        Assert.IsType<ColorEditor>(rows[5].Editor);
        Assert.IsType<TextBlock>(rows[6].Editor);
        Assert.IsType<Button>(rows[7].Editor);
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
        ((StackPanel)rows[2].Editor!).Children.OfType<RadioButton>().First().IsChecked = true;
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
