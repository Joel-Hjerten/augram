using System.Globalization;
using Augram.App.Components.CommandTree;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Augram.App.Components.StepList;

/// <summary>
/// Lookless row of the <see cref="StepList"/> (F5a): position, summary, type name, the F8 marker and
/// the active toggle; the selected row is <c>:expanded</c> and shows its type's parameter
/// <see cref="Form"/> in place (accordion). <c>:dragging</c> and <c>:drop-target</c> mark a reorder
/// in progress. The list remembers which step the form was built from and what it last emitted, so
/// an edit does not rebuild the form under the user's hands while an undo does. Two parts tell the list
/// where a press landed: <c>PART_Grip</c> (the drag handle and the number, <see cref="IsOnGrip"/>) and
/// <c>PART_FormHost</c> (around the form, <see cref="IsInForm"/>); the rest of the card is its header.
/// </summary>
[PseudoClasses(":expanded", ":dragging", ":drop-target")]
public sealed class StepRow : ItemRow
{
    public static readonly StyledProperty<StepItem?> ItemProperty =
        AvaloniaProperty.Register<StepRow, StepItem?>(nameof(Item));

    public static readonly StyledProperty<string> IndexTextProperty =
        AvaloniaProperty.Register<StepRow, string>(nameof(IndexText), string.Empty);

    public static readonly StyledProperty<string> SummaryTextProperty =
        AvaloniaProperty.Register<StepRow, string>(nameof(SummaryText), string.Empty);

    public static readonly StyledProperty<string> TypeTextProperty =
        AvaloniaProperty.Register<StepRow, string>(nameof(TypeText), string.Empty);

    public static readonly StyledProperty<string?> MarkerTextProperty =
        AvaloniaProperty.Register<StepRow, string?>(nameof(MarkerText));

    public static readonly StyledProperty<bool> HasMarkerProperty =
        AvaloniaProperty.Register<StepRow, bool>(nameof(HasMarker));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<StepRow, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<Control?> FormProperty =
        AvaloniaProperty.Register<StepRow, Control?>(nameof(Form));

    private Control? _grip;
    private Control? _formHost;

    public StepRow()
    {
        CanRename = false;
    }

    public StepItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public string IndexText
    {
        get => GetValue(IndexTextProperty);
        private set => SetValue(IndexTextProperty, value);
    }

    public string SummaryText
    {
        get => GetValue(SummaryTextProperty);
        private set => SetValue(SummaryTextProperty, value);
    }

    public string TypeText
    {
        get => GetValue(TypeTextProperty);
        private set => SetValue(TypeTextProperty, value);
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

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>The type's parameter form while expanded; null otherwise.</summary>
    public Control? Form
    {
        get => GetValue(FormProperty);
        set => SetValue(FormProperty, value);
    }

    /// <summary>The step <see cref="Form"/> was built from.</summary>
    public IStep? FormBuiltFor { get; set; }

    /// <summary>The last step the form handed to its <c>changed</c> callback; the list keeps the form when the store echoes it back.</summary>
    public IStep? LastEmitted { get; set; }

    public void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    public void SetDropTarget(bool target) => PseudoClasses.Set(":drop-target", target);

    /// <summary>
    /// Whether a press landed on the grip: on the header line, left of where <c>PART_Grip</c> (the drag handle and the
    /// number) ends, the card's edge before it included, so the whole left column drags and never opens the step.
    /// </summary>
    internal bool IsOnGrip(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return _grip is not null && !IsInForm(e.Source as Visual) && e.GetPosition(_grip).X < _grip.Bounds.Width;
    }

    /// <summary>
    /// Whether a pointer event landed in the open step's form (<c>PART_FormHost</c>, its padding included); the form keeps
    /// its presses. A press in one of its popups (a combo box's list) leaves the visual tree at the popup's root, so the
    /// walk goes on through the logical parent there.
    /// </summary>
    internal bool IsInForm(Visual? source)
    {
        for (var visual = source; visual is not null && visual != this; visual = visual.GetVisualParent() ?? (visual as ILogical)?.GetLogicalParent() as Visual)
        {
            if (ReferenceEquals(visual, _formHost))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grip = e.NameScope.Find<Control>("PART_Grip");
        _formHost = e.NameScope.Find<Control>("PART_FormHost");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            IndexText = item is null ? string.Empty : (item.Index + 1).ToString(CultureInfo.InvariantCulture) + ".";
            NameText = item?.Summary ?? string.Empty;
            SummaryText = item?.Summary ?? string.Empty;
            TypeText = item?.TypeName ?? string.Empty;
            MarkerText = item?.PlatformMarker;
            HasMarker = item?.HasMarker ?? false;
            IsActive = item?.IsActive ?? true;
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":expanded", IsExpanded);
        }
    }
}
