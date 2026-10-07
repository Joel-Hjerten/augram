using System.Globalization;
using Augram.App.Components.CommandTree;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Augram.App.Components.StepList;

/// <summary>
/// Lookless row of the <see cref="StepList"/> (F5a): position, summary, type name, the F8 marker and
/// the active toggle; the selected row is <c>:expanded</c> and shows its type's parameter
/// <see cref="Form"/> in place (accordion). <c>:dragging</c> and <c>:drop-target</c> mark a reorder
/// in progress. The list remembers which step the form was built from and what it last emitted, so
/// an edit does not rebuild the form under the user's hands while an undo does.
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
