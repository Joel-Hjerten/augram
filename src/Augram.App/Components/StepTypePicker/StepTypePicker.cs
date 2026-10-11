using System.Collections.ObjectModel;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.StepTypePicker;

/// <summary>
/// Lookless step type picker (F5a): <see cref="Types"/> and <see cref="Refusals"/> in, grouped by their declared
/// <see cref="StepCategory"/> (in enum order, types in registry order within a category; the <see cref="StepCategory.Other"/>
/// placeholders are never listed), one <see cref="TypeChosen"/> out. <see cref="Entries"/> is a category header followed by one
/// button per type; the template lists them. A type the host refuses (Joel, 0.11.3: a command whose only step is a Remap step
/// takes nothing more) is listed greyed, its button disabled with the reason as its tooltip, not hidden. Presentational: the
/// host decides the refusals (Core's <c>StepOffer</c>, through the view model); it knows no type key and no rule, so adding a
/// step type changes nothing here.
/// </summary>
public sealed class StepTypePicker : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<IStepType>> TypesProperty =
        AvaloniaProperty.Register<StepTypePicker, IReadOnlyList<IStepType>>(nameof(Types), []);

    public static readonly StyledProperty<IReadOnlyDictionary<IStepType, string>> RefusalsProperty =
        AvaloniaProperty.Register<StepTypePicker, IReadOnlyDictionary<IStepType, string>>(nameof(Refusals), ReadOnlyDictionary<IStepType, string>.Empty);

    public static readonly StyledProperty<IReadOnlyList<Control>> EntriesProperty =
        AvaloniaProperty.Register<StepTypePicker, IReadOnlyList<Control>>(nameof(Entries), []);

    public event EventHandler<IStepType>? TypeChosen;

    public IReadOnlyList<IStepType> Types
    {
        get => GetValue(TypesProperty);
        set => SetValue(TypesProperty, value);
    }

    /// <summary>The listed types the command cannot take now, each with the reason shown as its greyed button's tooltip; empty offers every type.</summary>
    public IReadOnlyDictionary<IStepType, string> Refusals
    {
        get => GetValue(RefusalsProperty);
        set => SetValue(RefusalsProperty, value);
    }

    public IReadOnlyList<Control> Entries
    {
        get => GetValue(EntriesProperty);
        private set => SetValue(EntriesProperty, value);
    }

    /// <summary>Every type listed, offered or greyed, in the order shown.</summary>
    public IReadOnlyList<IStepType> Listed => [.. TypeButtons.Select(button => (IStepType)button.Tag!)];

    /// <summary>The listed types that can be chosen, in the order shown.</summary>
    public IReadOnlyList<IStepType> Offered => [.. TypeButtons.Where(button => button.IsEnabled).Select(button => (IStepType)button.Tag!)];

    private IEnumerable<Button> TypeButtons => Entries.OfType<Button>();

    /// <summary>The types a picker lists of <paramref name="types"/>, in their order: all but the <see cref="StepCategory.Other"/> placeholders.</summary>
    public static IEnumerable<IStepType> Listable(IEnumerable<IStepType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return types.Where(type => type.Category != StepCategory.Other);
    }

    /// <summary>
    /// Why nothing of <paramref name="types"/> can be chosen under <paramref name="refusals"/>: the reason most of the listed types
    /// are refused with (the first in order on a tie), what "New step…" says while disabled; null while some type can be chosen
    /// or none is listed.
    /// </summary>
    public static string? NothingOffered(IEnumerable<IStepType> types, IReadOnlyDictionary<IStepType, string> refusals)
    {
        ArgumentNullException.ThrowIfNull(refusals);
        var listed = Listable(types).ToList();
        if (listed.Count == 0 || !listed.All(refusals.ContainsKey))
        {
            return null;
        }

        return listed.Select(type => refusals[type]).GroupBy(reason => reason).OrderByDescending(group => group.Count()).First().Key;
    }

    /// <summary>What a click on a type's button does; a refused type raises nothing.</summary>
    public void Choose(IStepType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!Refusals.ContainsKey(type))
        {
            TypeChosen?.Invoke(this, type);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TypesProperty || change.Property == RefusalsProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var entries = new List<Control>();
        foreach (var category in Listable(Types).GroupBy(type => type.Category).OrderBy(group => group.Key))
        {
            var header = new TextBlock { Text = category.Key.ToString() };
            header.Classes.Add("picker-category");
            entries.Add(header);
            foreach (var type in category)
            {
                var button = new Button { Content = type.DisplayName, Tag = type };
                button.Classes.Add("picker-type");
                if (Refusals.TryGetValue(type, out var reason))
                {
                    // Greyed, not hidden; Avalonia shows a disabled control's tooltip only when asked to.
                    button.IsEnabled = false;
                    ToolTip.SetTip(button, reason);
                    ToolTip.SetShowOnDisabled(button, true);
                }

                button.Click += (_, _) => Choose(type);
                entries.Add(button);
            }
        }

        Entries = entries;
    }
}
