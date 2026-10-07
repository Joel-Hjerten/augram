using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless header of the selected command (F5a, F3 "gesture picker from the command editor"): the
/// name, a trigger-kind dropdown (<c>PART_TriggerKind</c>: No trigger / Gesture / Wheel up / Wheel down)
/// and, for a gesture, a glyph button (<c>PART_PickGesture</c>) that asks the host to open the Select
/// Gesture picker. Choosing Gesture in the dropdown asks for the picker too; the dropdown is put back
/// to the command's real kind after every request, so a cancelled picker leaves it honest.
/// </summary>
public sealed class CommandHeader : TemplatedControl
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

    public static readonly StyledProperty<bool> IsGestureKindProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsGestureKind));

    private ComboBox? _kind;
    private bool _applying;

    public event EventHandler<CommandTreeActionEventArgs>? ActionRequested;

    /// <summary>The labels of the dropdown, in <see cref="TriggerKindExtensions.All"/> order.</summary>
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

    public bool IsGestureKind
    {
        get => GetValue(IsGestureKindProperty);
        private set => SetValue(IsGestureKindProperty, value);
    }

    /// <summary>The kind the dropdown shows; -1 without a command.</summary>
    public int KindIndex => _kind?.SelectedIndex ?? -1;

    /// <summary>What the dropdown does: asks the host for the kind (Gesture opens the picker).</summary>
    public void ChooseKind(TriggerKind kind)
    {
        if (Item is { } item && kind != item.TriggerKind)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, command: item, kind: kind));
        }

        ApplyKind();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _kind = e.NameScope.Find<ComboBox>("PART_TriggerKind");
        if (_kind is not null)
        {
            _kind.ItemsSource = KindLabels;
            ApplyKind();
            _kind.SelectionChanged += (_, _) =>
            {
                if (!_applying && _kind.SelectedIndex >= 0)
                {
                    ChooseKind(TriggerKindExtensions.All[_kind.SelectedIndex]);
                }
            };
        }

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
            ApplyKind();
        }
    }

    private void ApplyKind()
    {
        if (_kind is null)
        {
            return;
        }

        _applying = true;
        try
        {
            _kind.SelectedIndex = Item is { } item ? TriggerKindExtensions.All.ToList().IndexOf(item.TriggerKind) : -1;
        }
        finally
        {
            _applying = false;
        }
    }
}
