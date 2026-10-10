using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.StepTypePicker;

/// <summary>
/// Lookless step type picker (F5a): <see cref="Types"/> in, grouped by their declared
/// <see cref="StepCategory"/> (in enum order, types in registry order within a category; the
/// <see cref="StepCategory.Other"/> placeholders are never offered, and a type offered only under a hold remap
/// (<see cref="IStepType.HoldRemapsOnly"/>, the Remap step) only while <see cref="UnderHoldRemap"/>, plan 0002 step 4), one
/// <see cref="TypeChosen"/> out. <see cref="Entries"/> is a category header followed by one button per type; the template
/// lists them. Knows no type key: adding a step type changes nothing here.
/// </summary>
public sealed class StepTypePicker : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<IStepType>> TypesProperty =
        AvaloniaProperty.Register<StepTypePicker, IReadOnlyList<IStepType>>(nameof(Types), []);

    public static readonly StyledProperty<bool> UnderHoldRemapProperty =
        AvaloniaProperty.Register<StepTypePicker, bool>(nameof(UnderHoldRemap));

    public static readonly StyledProperty<IReadOnlyList<Control>> EntriesProperty =
        AvaloniaProperty.Register<StepTypePicker, IReadOnlyList<Control>>(nameof(Entries), []);

    public event EventHandler<IStepType>? TypeChosen;

    public IReadOnlyList<IStepType> Types
    {
        get => GetValue(TypesProperty);
        set => SetValue(TypesProperty, value);
    }

    /// <summary>The picker serves a command under a hold remap: the types offered only there (Remap) are offered too.</summary>
    public bool UnderHoldRemap
    {
        get => GetValue(UnderHoldRemapProperty);
        set => SetValue(UnderHoldRemapProperty, value);
    }

    public IReadOnlyList<Control> Entries
    {
        get => GetValue(EntriesProperty);
        private set => SetValue(EntriesProperty, value);
    }

    /// <summary>The types on offer, in the order shown.</summary>
    public IReadOnlyList<IStepType> Offered => [.. Entries.OfType<Button>().Select(button => (IStepType)button.Tag!)];

    /// <summary>What a click on a type's button does.</summary>
    public void Choose(IStepType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        TypeChosen?.Invoke(this, type);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TypesProperty || change.Property == UnderHoldRemapProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var entries = new List<Control>();
        var offered = Types.Where(type => type.Category != StepCategory.Other && (UnderHoldRemap || !type.HoldRemapsOnly));
        foreach (var category in offered.GroupBy(type => type.Category).OrderBy(group => group.Key))
        {
            var header = new TextBlock { Text = category.Key.ToString() };
            header.Classes.Add("picker-category");
            entries.Add(header);
            foreach (var type in category)
            {
                var button = new Button { Content = type.DisplayName, Tag = type };
                button.Classes.Add("picker-type");
                button.Click += (_, _) => Choose(type);
                entries.Add(button);
            }
        }

        Entries = entries;
    }
}
