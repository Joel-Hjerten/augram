using System.Collections.ObjectModel;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Components.StepList;

/// <summary>
/// The step type picker half of <see cref="StepList"/>. "New step…": the <see cref="StepTypePicker.StepTypePicker"/> in a flyout
/// under <c>PART_New</c>, kept in step with <see cref="StepList.StepTypes"/> and <see cref="StepRefusals"/> (the types the command
/// cannot take now, greyed with their reasons); a chosen type raises <see cref="StepListAction.Add"/>. When no listed type can be
/// added (the command's only step is a Remap step), <see cref="CanAddStep"/> is false and <see cref="NewStepRefusal"/> says why:
/// the template disables "New step…" with it as the tooltip, the menu's New step… is disabled and its key opens nothing. "Change
/// to…" (the menu, Joel 2026-10-11): a second picker of the same kind in a flyout at the step's header, greying that step's
/// <see cref="ChangeRefusals"/>; a chosen type raises <see cref="StepListAction.ChangeType"/> for the step.
/// </summary>
public sealed partial class StepList
{
    public static readonly StyledProperty<IReadOnlyDictionary<IStepType, string>> StepRefusalsProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyDictionary<IStepType, string>>(nameof(StepRefusals), ReadOnlyDictionary<IStepType, string>.Empty);

    public static readonly StyledProperty<IReadOnlyList<IReadOnlyDictionary<IStepType, string>>> ChangeRefusalsProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyList<IReadOnlyDictionary<IStepType, string>>>(nameof(ChangeRefusals), []);

    public static readonly DirectProperty<StepList, bool> CanAddStepProperty =
        AvaloniaProperty.RegisterDirect<StepList, bool>(nameof(CanAddStep), list => list.CanAddStep);

    public static readonly DirectProperty<StepList, string?> NewStepRefusalProperty =
        AvaloniaProperty.RegisterDirect<StepList, string?>(nameof(NewStepRefusal), list => list.NewStepRefusal);

    private StepTypePicker.StepTypePicker? _picker;
    private Flyout? _flyout;
    private StepTypePicker.StepTypePicker? _changePicker;
    private Flyout? _changeFlyout;
    private StepItem? _changing;
    private bool _canAddStep;
    private string? _newStepRefusal;

    /// <summary>
    /// The step types the command cannot take a step of now, each with its reason; the picker greys them. The workbench sets it
    /// from the selected command's <c>CommandHeaderItem.StepRefusals</c> (Core's <c>StepOffer</c>); empty offers every type.
    /// </summary>
    public IReadOnlyDictionary<IStepType, string> StepRefusals
    {
        get => GetValue(StepRefusalsProperty);
        set => SetValue(StepRefusalsProperty, value);
    }

    /// <summary>
    /// By step index, the types each step cannot be changed to now, each with its reason (its own type among them); "Change to…"
    /// greys them for the step it changes. The workbench sets it from the selected command's <c>CommandHeaderItem.ChangeRefusals</c>
    /// (Core's <c>StepOffer.ChangeRefusals</c>); a step it has no entry for is offered every type.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<IStepType, string>> ChangeRefusals
    {
        get => GetValue(ChangeRefusalsProperty);
        set => SetValue(ChangeRefusalsProperty, value);
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
    public StepTypePicker.StepTypePicker TypePicker => _picker ??= BuildPicker(StepRefusals, ChooseNew);

    /// <summary>The picker "Change to…" shows for the step it was opened for (<see cref="OpenChangePicker"/>); built on first use.</summary>
    public StepTypePicker.StepTypePicker ChangeTypePicker => _changePicker ??= BuildPicker(ChangeRefusalsOf(_changing), ChooseChange);

    public void OpenTypePicker()
    {
        if (_new is null)
        {
            return;
        }

        _flyout ??= new Flyout { Content = TypePicker, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _flyout.ShowAt(_new);
    }

    /// <summary>
    /// Points <see cref="ChangeTypePicker"/> at <paramref name="step"/> (its <see cref="ChangeRefusals"/> greyed) and shows it in a
    /// flyout under the step's header line (<see cref="StepRow.HeaderPart"/>), so it opens over the step's own form.
    /// </summary>
    public void OpenChangePicker(StepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _changing = step;
        ChangeTypePicker.Refusals = ChangeRefusalsOf(step);
        if (Rows.FirstOrDefault(row => row.Item?.Index == step.Index) is not { } row)
        {
            return;
        }

        _changeFlyout ??= new Flyout { Content = ChangeTypePicker, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _changeFlyout.ShowAt(row.HeaderPart ?? row);
    }

    private IReadOnlyDictionary<IStepType, string> ChangeRefusalsOf(StepItem? step)
        => step is { Index: var index } && index >= 0 && index < ChangeRefusals.Count ? ChangeRefusals[index] : ReadOnlyDictionary<IStepType, string>.Empty;

    private void SyncPicker(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ChangeRefusalsProperty)
        {
            if (_changePicker is not null)
            {
                _changePicker.Refusals = ChangeRefusalsOf(_changing);
            }

            return;
        }

        if (change.Property != StepTypesProperty && change.Property != StepRefusalsProperty && change.Property != HasCommandProperty)
        {
            return;
        }

        NewStepRefusal = HasCommand ? StepTypePicker.StepTypePicker.NothingOffered(StepTypes, StepRefusals) : null;
        CanAddStep = HasCommand && NewStepRefusal is null;
        if (change.Property == StepTypesProperty)
        {
            SetTypes(_picker);
            SetTypes(_changePicker);
        }
        else if (change.Property == StepRefusalsProperty && _picker is not null)
        {
            _picker.Refusals = StepRefusals;
        }
    }

    private void SetTypes(StepTypePicker.StepTypePicker? picker)
    {
        if (picker is not null)
        {
            picker.Types = StepTypes;
        }
    }

    private StepTypePicker.StepTypePicker BuildPicker(IReadOnlyDictionary<IStepType, string> refusals, Action<IStepType> chosen)
    {
        var picker = new StepTypePicker.StepTypePicker { Types = StepTypes, Refusals = refusals };
        picker.TypeChosen += (_, type) => chosen(type);
        return picker;
    }

    private void ChooseNew(IStepType type)
    {
        _flyout?.Hide();
        Raise(StepListAction.Add, null, type: type);
    }

    private void ChooseChange(IStepType type)
    {
        _changeFlyout?.Hide();
        if (_changing is { } step)
        {
            Raise(StepListAction.ChangeType, step, type: type);
        }
    }
}
