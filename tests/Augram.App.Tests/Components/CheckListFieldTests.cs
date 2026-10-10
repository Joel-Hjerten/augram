using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The check-list field kind (plan 0003, the export dialog's selection): one box per item, one under the other, each two-way on its own binding, its detail beside the caption.</summary>
public sealed class CheckListFieldTests
{
    [AvaloniaFact]
    public void EachItemIsACheckBoxWithItsCaptionAndDetail_InAFramedList()
    {
        var (row, _, _) = Show();

        var frame = Assert.IsType<Border>(row.Editor);
        Assert.Contains("check-list", frame.Classes);
        var boxes = Boxes(row);
        Assert.Equal(2, boxes.Count);
        Assert.Equal(["Global", "7 commands"], Texts(boxes[0]));
        Assert.Equal(["Blender"], Texts(boxes[1]));
        Assert.Equal([true, false], boxes.Select(box => box.IsChecked == true));
        Assert.Contains(boxes[0].GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "7 commands" && text.Classes.Contains("help"));
    }

    [AvaloniaFact]
    public void TickingABoxSetsItsOwnBinding_AndABindingChangeTicksTheBox()
    {
        var (row, global, blender) = Show();
        var boxes = Boxes(row);

        boxes[1].IsChecked = true;
        Assert.True(blender.Value);
        Assert.True(global.Value);

        global.Value = false;
        global.Owner.Raise();
        Assert.False(boxes[0].IsChecked);
        Assert.True(boxes[1].IsChecked);
    }

    private static (FieldRow Row, Flag Global, Flag Blender) Show()
    {
        var global = new Flag(true);
        var blender = new Flag(false);
        var field = new CheckListField("App groups", [new CheckListItem("Global", global.Binding, "7 commands"), new CheckListItem("Blender", blender.Binding)]);
        var form = new SectionForm { Screen = new FormScreen("Check list", [new Section("Section", [field])]) };
        new Window { Width = 600, Height = 400, Content = form }.Show();
        return (form.GetVisualDescendants().OfType<FieldRow>().Single(), global, blender);
    }

    private static List<CheckBox> Boxes(FieldRow row) => [.. row.GetVisualDescendants().OfType<CheckBox>()];

    private static List<string?> Texts(CheckBox box) => [.. box.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).Where(text => !string.IsNullOrEmpty(text))];

    /// <summary>One bool with an observable owner, so a change made elsewhere reaches the box.</summary>
    private sealed class Flag
    {
        public Flag(bool value)
        {
            Value = value;
            Owner = new FakeOwner();
            Binding = new DelegateBinding<bool>(() => Value, set => Value = set, Owner, propertyName: "Value");
        }

        public bool Value { get; set; }

        public FakeOwner Owner { get; }

        public DelegateBinding<bool> Binding { get; }
    }

    private sealed class FakeOwner : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void Raise() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Value"));
    }
}
