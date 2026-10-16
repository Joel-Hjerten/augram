using System.Globalization;
using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.HeldInputList;

/// <summary>
/// Lookless row of the <see cref="HeldInputList"/> (plan 0012), drawn as a step row is: the grip and position
/// (<c>PART_Grip</c>; <see cref="IsOnGrip"/>), the input's name, its F8 marker and the hold-key warning under the name. A row
/// of the other platform's side is <c>:readonly</c> (no grip) and shows <c>PART_CopyTo</c> ("Copy to Windows") while its list
/// says so, raising <see cref="CopyToRequested"/>. <c>:dragging</c> marks the row being dragged
/// (<see cref="RowDragReorder{TRow}"/>). No rename, no active box.
/// </summary>
[PseudoClasses(":dragging", ":readonly")]
public sealed class HeldInputRow : ItemRow, IReorderRow
{
    public static readonly StyledProperty<HeldInputItem?> ItemProperty =
        AvaloniaProperty.Register<HeldInputRow, HeldInputItem?>(nameof(Item));

    public static readonly StyledProperty<string> IndexTextProperty =
        AvaloniaProperty.Register<HeldInputRow, string>(nameof(IndexText), string.Empty);

    public static readonly StyledProperty<string?> MarkerTextProperty =
        AvaloniaProperty.Register<HeldInputRow, string?>(nameof(MarkerText));

    public static readonly StyledProperty<bool> HasMarkerProperty =
        AvaloniaProperty.Register<HeldInputRow, bool>(nameof(HasMarker));

    public static readonly StyledProperty<string?> WarningTextProperty =
        AvaloniaProperty.Register<HeldInputRow, string?>(nameof(WarningText));

    public static readonly StyledProperty<bool> HasWarningProperty =
        AvaloniaProperty.Register<HeldInputRow, bool>(nameof(HasWarning));

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<HeldInputRow, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<string?> CopyToTextProperty =
        AvaloniaProperty.Register<HeldInputRow, string?>(nameof(CopyToText));

    private Control? _grip;

    public HeldInputRow()
    {
        CanRename = false;
    }

    /// <summary>The row's Copy to button was clicked (a row of the other platform's side).</summary>
    public event EventHandler? CopyToRequested;

    public HeldInputItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public string IndexText
    {
        get => GetValue(IndexTextProperty);
        private set => SetValue(IndexTextProperty, value);
    }

    public string? MarkerText
    {
        get => GetValue(MarkerTextProperty);
        private set => SetValue(MarkerTextProperty, value);
    }

    public bool HasMarker
    {
        get => GetValue(HasMarkerProperty);
        private set => SetValue(HasMarkerProperty, value);
    }

    public string? WarningText
    {
        get => GetValue(WarningTextProperty);
        private set => SetValue(WarningTextProperty, value);
    }

    public bool HasWarning
    {
        get => GetValue(HasWarningProperty);
        private set => SetValue(HasWarningProperty, value);
    }

    /// <summary>A row of the other platform's side (plan 0012 decision 5): <c>:readonly</c>, no grip.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>"Copy to Windows" on a row of the other platform's side; null hides the button.</summary>
    public string? CopyToText
    {
        get => GetValue(CopyToTextProperty);
        set => SetValue(CopyToTextProperty, value);
    }

    /// <summary>How far a drag in progress shows this row from its place, in pixels; 0 at rest.</summary>
    public double DragOffset { get; private set; }

    int IReorderRow.ReorderIndex => Item?.Index ?? -1;

    double IReorderRow.DragOffset { get => DragOffset; set => DragOffset = value; }

    public void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    /// <summary>Whether a press landed on the grip (the handle and the number, the card's edge before them included).</summary>
    internal bool IsOnGrip(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return _grip is not null && e.GetPosition(_grip).X < _grip.Bounds.Width;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grip = e.NameScope.Find<Control>("PART_Grip");
        e.OnClick("PART_CopyTo", () => CopyToRequested?.Invoke(this, EventArgs.Empty));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            IndexText = item is null ? string.Empty : (item.Index + 1).ToString(CultureInfo.InvariantCulture) + ".";
            NameText = item?.Name ?? string.Empty;
            MarkerText = item?.Marker;
            HasMarker = item?.HasMarker ?? false;
            WarningText = item?.Warning;
            HasWarning = item?.HasWarning ?? false;
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(":readonly", IsReadOnly);
        }
    }
}
