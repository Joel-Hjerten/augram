using System.Collections.ObjectModel;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Components.StepList;

/// <summary>
/// The "New step…" half of <see cref="StepList"/>: the <see cref="StepTypePicker.StepTypePicker"/> in a flyout under
/// <c>PART_New</c>, kept in step with <see cref="StepList.StepTypes"/> and <see cref="StepRefusals"/> (the types the command
/// cannot take now, greyed with their reasons); a chosen type raises <see cref="StepListAction.Add"/>. When no listed type can
/// be added (the command's only step is a Remap step), <see cref="CanAddStep"/> is false and <see cref="NewStepRefusal"/> says
/// why: the template disables "New step…" with it as the tooltip, the menu's New step… is disabled and its key opens nothing.
/// </summary>
public sealed partial class StepList
{
    public static readonly StyledProperty<IReadOnlyDictionary<IStepType, string>> StepRefusalsProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyDictionary<IStepType, string>>(nameof(StepRefusals), ReadOnlyDictionary<IStepType, string>.Empty);

    public static readonly DirectProperty<StepList, bool> CanAddStepProperty =
        AvaloniaProperty.RegisterDirect<StepList, bool>(nameof(CanAddStep), list => list.CanAddStep);

    public static readonly DirectProperty<StepList, string?> NewStepRefusalProperty =
        AvaloniaProperty.RegisterDirect<StepList, string?>(nameof(NewStepRefusal), list => list.NewStepRefusal);

    private StepTypePicker.StepTypePicker? _picker;
    private Flyout? _flyout;
    private bool _canAddStep;
    private string? _newStepRefusal;

    /// <summary>
    /// The step types the command cannot take a step of now, each with its reason; the picker greys them. The workbench sets it
    /// from the selected command's <c>CommandItem.StepRefusals</c> (Core's <c>StepOffer</c>); empty offers every type.
    /// </summary>
    public IReadOnlyDictionary<IStepType, string> StepRefusals
    {
        get => GetValue(StepRefusalsProperty);
        set => SetValue(StepRefusalsProperty, value);
    }

    /// <summary>"New step…" is enabled: a command is selected and some listed type can be added to it.</summary>
    public bool CanAddStep
    {
        get => _canAddStep;
        private set => SetAndRaise(CanAddStepProperty, ref _canAddStep, value);
    }

    /// <summary>Why "New step…" is disabled though a command is selected (the reason most types are greyed with); null otherwise.</summary>
    public string? NewStepRefusal
    {
        get => _newStepRefusal;
        private set => SetAndRaise(NewStepRefusalProperty, ref _newStepRefusal, value);
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
        if (change.Property != StepTypesProperty && change.Property != StepRefusalsProperty && change.Property != HasCommandProperty)
        {
            return;
        }

        NewStepRefusal = HasCommand ? StepTypePicker.StepTypePicker.NothingOffered(StepTypes, StepRefusals) : null;
        CanAddStep = HasCommand && NewStepRefusal is null;
        if (_picker is null)
        {
            return;
        }

        if (change.Property == StepTypesProperty)
        {
            _picker.Types = StepTypes;
        }
        else if (change.Property == StepRefusalsProperty)
        {
            _picker.Refusals = StepRefusals;
        }
    }

    private StepTypePicker.StepTypePicker BuildPicker()
    {
        var picker = new StepTypePicker.StepTypePicker { Types = StepTypes, Refusals = StepRefusals };
        picker.TypeChosen += (_, type) =>
        {
            _flyout?.Hide();
            Raise(StepListAction.Add, null, type: type);
        };
        return picker;
    }
}
