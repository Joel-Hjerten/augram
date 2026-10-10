using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless header of the selected command (F5a, F3 "gesture picker from the command editor"): the
/// name, a trigger-kind dropdown (<c>PART_TriggerKind</c>: Gesture / Wheel / Button / No trigger; the wheel's direction and the
/// "While holding" boxes are <c>CommandHeader.Hold.cs</c>, a button trigger's pressed button <c>CommandHeader.Button.cs</c>),
/// for a gesture a glyph button (<c>PART_PickGesture</c>) that asks the host to open the Select Gesture
/// picker, and a Category dropdown (<c>PART_Category</c>) when the item offers categories
/// (<see cref="CommandItem.Categories"/>: always on the Global tab, only for a group that has some on the
/// Apps tab). Choosing Gesture in the kind dropdown asks for the picker too. Both dropdowns only ask
/// (<see cref="AskingDropdown"/>): they are put back to the item's value after every request, so a cancelled
/// picker or a refused category leaves them honest; for the trigger that value is the host's draft while a
/// combination is not valid yet (<see cref="CommandItem.DraftNote"/>, shown as <c>DraftNote</c>). The Use on
/// boxes (<c>PART_UseOnWindows</c>, <c>PART_UseOnMac</c>, F8) ask the same way; a platform the command's category or app group leaves out shows its box
/// unchecked and disabled, with <see cref="UseOnNote"/> saying which one (Joel, 2026-10-08). A command under a hold remap
/// shows its Input instead of the trigger kind and the "While holding" boxes (<c>CommandHeader.Input.cs</c>, plan 0002). A
/// trigger whose held buttons are handed back as drags has a Drag distance row (<c>CommandHeader.DragDistance.cs</c>), and a
/// command not under a hold remap a Not in row (<c>CommandHeader.NotIn.cs</c>; both plan 0004). A draft another command uses
/// here has Take it and Swap in its note (<c>CommandHeader.Conflict.cs</c>, Joel 2026-10-10).
/// </summary>
public sealed partial class CommandHeader : TemplatedControl
{
    public static readonly StyledProperty<CommandItem?> ItemProperty =
        AvaloniaProperty.Register<CommandHeader, CommandItem?>(nameof(Item));

    public static readonly StyledProperty<bool> HasCommandProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasCommand));

    public static readonly StyledProperty<string> NameTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(NameText), string.Empty);

    public static readonly StyledProperty<string> TriggerTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(TriggerText), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<CommandHeader, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<string?> TriggerHintProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(TriggerHint));

    public static readonly StyledProperty<bool> IsGestureKindProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsGestureKind));

    public static readonly StyledProperty<string?> VersionTextProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(VersionText));

    public static readonly StyledProperty<bool> HasOwnVersionHereProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasOwnVersionHere));

    public static readonly StyledProperty<bool> CanMarkCheckedProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanMarkChecked));

    public static readonly StyledProperty<bool> HasCategoriesProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasCategories));

    public static readonly StyledProperty<string?> UseOnNoteProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(UseOnNote));

    private AskingDropdown? _kind;
    private AskingDropdown? _category;

    private CheckBox? _useOnWindows;
    private CheckBox? _useOnMac;
    private bool _applying;

    public event EventHandler<CommandTreeActionEventArgs>? ActionRequested;

    /// <summary>The labels of the kind dropdown, in <see cref="TriggerKindExtensions.All"/> order.</summary>
    public static IReadOnlyList<string> KindLabels { get; } = [.. TriggerKindExtensions.All.Select(kind => kind.Label())];

    public CommandItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public bool HasCommand
    {
        get => GetValue(HasCommandProperty);
        private set => SetValue(HasCommandProperty, value);
    }

    public string NameText
    {
        get => GetValue(NameTextProperty);
        private set => SetValue(NameTextProperty, value);
    }

    public string TriggerText
    {
        get => GetValue(TriggerTextProperty);
        private set => SetValue(TriggerTextProperty, value);
    }

    public IReadOnlyList<GesturePoint>? Points
    {
        get => GetValue(PointsProperty);
        private set => SetValue(PointsProperty, value);
    }

    /// <summary>How the trigger fires, below it, for the kinds that are not obvious (a wheel or a click trigger); null otherwise.</summary>
    public string? TriggerHint
    {
        get => GetValue(TriggerHintProperty);
        private set => SetValue(TriggerHintProperty, value);
    }

    public bool IsGestureKind
    {
        get => GetValue(IsGestureKindProperty);
        private set => SetValue(IsGestureKindProperty, value);
    }

    /// <summary>Shows the Category dropdown: the item offers at least one choice.</summary>
    public bool HasCategories
    {
        get => GetValue(HasCategoriesProperty);
        private set => SetValue(HasCategoriesProperty, value);
    }

    /// <summary>
    /// F8: why a Use on box is disabled, beside the boxes (<see cref="CommandItem.UseOnLimitText"/>: "Set by category 'Personal':
    /// Windows only"); null when the command's group and category leave it every platform.
    /// </summary>
    public string? UseOnNote
    {
        get => GetValue(UseOnNoteProperty);
        private set => SetValue(UseOnNoteProperty, value);
    }

    /// <summary>F8: where this platform's steps come from and what an edit here does (<see cref="CommandItem.VersionText"/>).</summary>
    public string? VersionText
    {
        get => GetValue(VersionTextProperty);
        private set => SetValue(VersionTextProperty, value);
    }

    /// <summary>F8: this platform runs its own steps; shows "Use the converted original".</summary>
    public bool HasOwnVersionHere
    {
        get => GetValue(HasOwnVersionHereProperty);
        private set => SetValue(HasOwnVersionHereProperty, value);
    }

    /// <summary>F8: the own steps here were made before the original last changed; shows "Mark as checked".</summary>
    public bool CanMarkChecked
    {
        get => GetValue(CanMarkCheckedProperty);
        private set => SetValue(CanMarkCheckedProperty, value);
    }

    /// <summary>The kind the dropdown shows; -1 without a command.</summary>
    public int KindIndex => _kind?.SelectedIndex ?? -1;

    /// <summary>The category the dropdown shows, as an index into <see cref="CommandItem.Categories"/>; -1 without a command.</summary>
    public int CategoryIndex => _category?.SelectedIndex ?? -1;

    /// <summary>What the kind dropdown does: asks the host for the kind (Gesture opens the picker).</summary>
    public void ChooseKind(TriggerKind kind)
    {
        if (Item is { } item && kind != item.TriggerKind)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, command: item, kind: kind));
        }

        Apply();
    }

    /// <summary>What the Category dropdown does: asks the host to move the command into the category.</summary>
    public void ChooseCategory(CategoryChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (Item is { } item && choice.Id != item.CategoryId)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: item, category: choice));
        }

        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<ComboBox>("PART_TriggerKind") is { } kind)
        {
            _kind = new AskingDropdown(kind, index => ChooseKind(TriggerKindExtensions.All[index]));
        }

        if (e.NameScope.Find<ComboBox>("PART_Category") is { } category)
        {
            _category = new AskingDropdown(category, index =>
            {
                if (Item is { } item && index < item.Categories.Count)
                {
                    ChooseCategory(item.Categories[index]);
                }
            });
        }

        _useOnWindows = e.NameScope.Find<CheckBox>("PART_UseOnWindows");
        _useOnMac = e.NameScope.Find<CheckBox>("PART_UseOnMac");
        FindHoldParts(e);
        FindButtonParts(e);
        FindDragDistanceParts(e);
        FindNotInParts(e);
        FindInputParts(e);
        FindConflictParts(e);
        WireUseOn(_useOnWindows, HostPlatform.Windows);
        WireUseOn(_useOnMac, HostPlatform.MacOS);
        Apply();
        WireAction(e, "PART_UseConverted", CommandTreeAction.UseConvertedOriginal);
        WireAction(e, "PART_MarkChecked", CommandTreeAction.MarkOwnVersionChecked);
        if (e.NameScope.Find<Button>("PART_PickGesture") is { } pick)
        {
            pick.Click += (_, _) =>
            {
                if (Item is { } item)
                {
                    ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.PickGesture, command: item));
                }
            };
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            HasCommand = item is not null;
            NameText = item?.Name ?? string.Empty;
            TriggerText = item?.TriggerText ?? string.Empty;
            Points = item?.GlyphPoints;
            IsGestureKind = item?.TriggerKind == TriggerKind.Gesture;
            TriggerHint = item?.TriggerHint;
            AnchorWarning = item?.AnchorWarning;
            TriggerNote = item?.TriggerNote;
            DraftNote = item?.DraftNote;
            ShowConflict(item);
            HasCategories = item is { Categories.Count: > 0 };
            VersionText = item?.VersionText;
            UseOnNote = item?.UseOnLimitText;
            HasOwnVersionHere = item is { HasOwnVersionHere: true };
            CanMarkChecked = item is { HasOwnVersionHere: true, IsOwnVersionStale: true };
            Apply();
        }
    }

    private void WireAction(TemplateAppliedEventArgs e, string part, CommandTreeAction action)
    {
        if (e.NameScope.Find<Button>(part) is { } button)
        {
            button.Click += (_, _) =>
            {
                if (Item is { } item)
                {
                    ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, command: item));
                }
            };
        }
    }

    /// <summary>
    /// Puts both dropdowns and the Use on boxes on the command's real values: a box is checked when the command is used there,
    /// and disabled (unchecked) when its group or category leaves that platform out.
    /// </summary>
    private void Apply()
    {
        var item = Item;
        _applying = true;
        try
        {
            ShowUseOn(_useOnWindows, item, HostPlatform.Windows);
            ShowUseOn(_useOnMac, item, HostPlatform.MacOS);
        }
        finally
        {
            _applying = false;
        }

        _kind?.Show(KindLabels, item is null ? -1 : TriggerKindExtensions.All.ToList().IndexOf(item.TriggerKind));
        ApplyHold();
        ApplyDragDistance();
        ApplyNotIn();
        ApplyInput();
        var choices = item?.Categories ?? [];
        _category?.Show([.. choices.Select(choice => choice.Name)], item is null ? -1 : IndexOfCategory(item));
    }

    /// <summary>The command's category, or Uncategorized when it names none the group still has.</summary>
    private static int IndexOfCategory(CommandItem item)
    {
        var choices = item.Categories.ToList();
        var index = choices.FindIndex(choice => choice.Id == item.CategoryId);
        return index >= 0 ? index : choices.FindIndex(choice => choice.Id is null);
    }
}
