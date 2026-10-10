using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Components.StepList;

/// <summary>
/// The "New step…" half of <see cref="StepList"/>: the <see cref="StepTypePicker.StepTypePicker"/> in a flyout under
/// <c>PART_New</c>, kept in step with <see cref="StepList.StepTypes"/> and <see cref="UnderHoldRemap"/> (plan 0002: a command
/// under a hold remap is offered the Remap step too); a chosen type raises <see cref="StepListAction.Add"/>.
/// </summary>
public sealed partial class StepList
{
    public static readonly StyledProperty<bool> UnderHoldRemapProperty =
        AvaloniaProperty.Register<StepList, bool>(nameof(UnderHoldRemap));

    private StepTypePicker.StepTypePicker? _picker;
    private Flyout? _flyout;

    /// <summary>The command is under a hold remap (plan 0002): "New step…" offers the Remap step too.</summary>
    public bool UnderHoldRemap
    {
        get => GetValue(UnderHoldRemapProperty);
        set => SetValue(UnderHoldRemapProperty, value);
    }

    /// <summary>The picker "New step…" shows; built on first use so tests can choose a type without a flyout.</summary>
    public StepTypePicker.StepTypePicker TypePicker => _picker ??= BuildPicker();

    public void OpenTypePicker()
    {
        if (_new is null)
        {
            return;
        }

        _flyout ??= new Flyout { Content = TypePicker, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _flyout.ShowAt(_new);
    }

    private void SyncPicker(AvaloniaPropertyChangedEventArgs change)
    {
        if (_picker is null)
        {
            return;
        }

        if (change.Property == StepTypesProperty)
        {
            _picker.Types = StepTypes;
        }
        else if (change.Property == UnderHoldRemapProperty)
        {
            _picker.UnderHoldRemap = UnderHoldRemap;
        }
    }

    private StepTypePicker.StepTypePicker BuildPicker()
    {
        var picker = new StepTypePicker.StepTypePicker { Types = StepTypes, UnderHoldRemap = UnderHoldRemap };
        picker.TypeChosen += (_, type) =>
        {
            _flyout?.Hide();
            Raise(StepListAction.Add, null, type: type);
        };
        return picker;
    }
}
