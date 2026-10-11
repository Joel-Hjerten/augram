using Augram.App.Components.Fields.Slider;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>
/// The slider field (plan 0006: Tint, Corner rounding): a slider and its value with the unit beside it, two-way through the
/// binding, and <see cref="Field.Enabled"/>, which disables the editor while it reads false (Tint under Solid).
/// </summary>
public sealed class SliderFieldTests
{
    [AvaloniaFact]
    public void TheSliderAndItsValueFollowTheBinding_AndAMoveIsWrittenBack()
    {
        var vm = new FakeOptions { Amount = 62 };
        var (slider, value, _) = Show(new SliderField("Tint", new DelegateBinding<double>(() => vm.Amount, v => vm.Amount = v, vm), 0, 100, 1, "%"));

        Assert.Equal((0, 100, 1), (slider.Minimum, slider.Maximum, slider.TickFrequency));
        Assert.True(slider.IsSnapToTickEnabled);
        Assert.Equal(62, slider.Value);
        Assert.Equal("62%", value.Text);

        vm.Amount = 30;
        Assert.Equal(30, slider.Value);
        Assert.Equal("30%", value.Text);

        slider.Value = 75;
        Assert.Equal(75, vm.Amount);
        Assert.Equal("75%", value.Text);
    }

    [AvaloniaFact]
    public void ShowingTheFormWritesNothingBack_EvenAValueOutOfRange()
    {
        var vm = new FakeOptions { Amount = 30 };
        var changes = 0;
        vm.PropertyChanged += (_, _) => changes++;

        var (slider, value, _) = Show(new SliderField("Corner rounding", new DelegateBinding<double>(() => vm.Amount, v => vm.Amount = v, vm), 4, 18, 1, " px"));

        Assert.Equal(18, slider.Value);
        Assert.Equal("30 px", value.Text);
        Assert.Equal(30, vm.Amount);
        Assert.Equal(0, changes);
    }

    [AvaloniaFact]
    public void AnEnabledBindingDisablesTheEditorWhileFalse_TheRowStays()
    {
        var vm = new FakeOptions { Flag = false };
        var field = new SliderField("Tint", new DelegateBinding<double>(() => vm.Amount, v => vm.Amount = v, vm), 0, 100, 1, "%")
        {
            Enabled = new DelegateBinding<bool>(() => vm.Flag, owner: vm),
        };
        var (slider, value, row) = Show(field);

        // The value beside it is disabled too (the theme mutes it); the row, its label and its help stay as they are.
        Assert.False(slider.IsEffectivelyEnabled);
        Assert.False(value.IsEffectivelyEnabled);
        Assert.True(row.IsVisible);
        Assert.True(row.IsEffectivelyEnabled);

        vm.Flag = true;
        Assert.True(slider.IsEffectivelyEnabled);
        Assert.True(value.IsEffectivelyEnabled);

        vm.Flag = false;
        Assert.False(slider.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void EnabledWorksOnAnyKind_AndNeverEnablesAReadOnlyEditor()
    {
        var vm = new FakeOptions { Flag = true };
        var enabled = new DelegateBinding<bool>(() => vm.Flag, owner: vm);
        var screen = new FormScreen("Test",
        [
            new Section("Kinds",
            [
                new NumberField("Number", new DelegateBinding<double>(() => vm.Amount, v => vm.Amount = v, vm), 0, 100) { Enabled = enabled },
                new SliderField("Read-only", new DelegateBinding<double>(() => vm.Amount, owner: vm), 0, 100) { Enabled = enabled },
            ]),
        ]);
        var form = new SectionForm { Screen = screen };
        new Window { Content = form }.Show();
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();

        Assert.True(rows[0].Editor!.IsEnabled);
        Assert.False(rows[1].Editor!.IsEnabled);

        vm.Flag = false;
        Assert.False(rows[0].Editor!.IsEnabled);
        vm.Flag = true;
        Assert.True(rows[0].Editor!.IsEnabled);
        Assert.False(rows[1].Editor!.IsEnabled);
    }

    [Fact]
    public void TheValueTextIsTheNumberThenTheUnit()
    {
        Assert.Equal("62%", SliderFieldRenderer.ValueText(62, "%"));
        Assert.Equal("12 px", SliderFieldRenderer.ValueText(12, " px"));
        Assert.Equal("0.5", SliderFieldRenderer.ValueText(0.5, string.Empty));
    }

    private static (Slider Slider, TextBlock Value, FieldRow Row) Show(SliderField field)
    {
        var form = new SectionForm { Screen = new FormScreen("Test", [new Section("Section", [field])]) };
        new Window { Content = form, Width = 700, Height = 300 }.Show();
        var row = form.GetVisualDescendants().OfType<FieldRow>().Single();
        var line = Assert.IsType<StackPanel>(row.Editor);
        Assert.Contains("slider-line", line.Classes);
        return (line.Children.OfType<Slider>().Single(), line.Children.OfType<TextBlock>().Single(text => text.Classes.Contains("slider-value")), row);
    }
}
