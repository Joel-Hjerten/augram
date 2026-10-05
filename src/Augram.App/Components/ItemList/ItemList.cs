using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;

namespace Augram.App.Components.ItemList;

/// <summary>
/// Renders a <see cref="ListSpec"/> (ADR-0002 §5c): title, filters and toolbar actions above, a
/// header row, then one row per visible item in <c>PART_Rows</c>. Filtering is applied here from the
/// spec's <see cref="ListFilter"/>s; rows refresh (coalesced, on the UI thread) whenever the source
/// changes. The row visuals are built by <see cref="ListRowBuilder"/>.
/// </summary>
public sealed class ItemList : TemplatedControl
{
    public static readonly StyledProperty<ListSpec?> SpecProperty =
        AvaloniaProperty.Register<ItemList, ListSpec?>(nameof(Spec));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ItemList, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<Control>> ToolbarItemsProperty =
        AvaloniaProperty.Register<ItemList, IReadOnlyList<Control>>(nameof(ToolbarItems), []);

    public static readonly StyledProperty<Control?> HeaderRowProperty =
        AvaloniaProperty.Register<ItemList, Control?>(nameof(HeaderRow));

    public static readonly StyledProperty<IReadOnlyList<object>> RowsProperty =
        AvaloniaProperty.Register<ItemList, IReadOnlyList<object>>(nameof(Rows), []);

    public static readonly StyledProperty<IDataTemplate?> RowTemplateProperty =
        AvaloniaProperty.Register<ItemList, IDataTemplate?>(nameof(RowTemplate));

    private ListBox? _rows;
    private ListSpec? _subscribed;
    private int _refreshQueued;

    public ListSpec? Spec
    {
        get => GetValue(SpecProperty);
        set => SetValue(SpecProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        private set => SetValue(TitleProperty, value);
    }

    public IReadOnlyList<Control> ToolbarItems
    {
        get => GetValue(ToolbarItemsProperty);
        private set => SetValue(ToolbarItemsProperty, value);
    }

    public Control? HeaderRow
    {
        get => GetValue(HeaderRowProperty);
        private set => SetValue(HeaderRowProperty, value);
    }

    /// <summary>The items currently shown, after filters.</summary>
    public IReadOnlyList<object> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    public IDataTemplate? RowTemplate
    {
        get => GetValue(RowTemplateProperty);
        private set => SetValue(RowTemplateProperty, value);
    }

    public object? SelectedItem => _rows?.SelectedItem;

    /// <summary>Re-reads the source and re-applies filters now, on the calling (UI) thread.</summary>
    public void Refresh()
    {
        var spec = Spec;
        if (spec is null)
        {
            Rows = [];
            return;
        }

        var filters = spec.Filters ?? [];
        var visible = spec.Source.Items.Where(item => filters.All(filter => filter.Matches(item, filter.Selected.Get()))).ToList();
        Rows = visible;
        if (spec.AutoScroll && visible.Count > 0)
        {
            _rows?.ScrollIntoView(visible[^1]);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rows = e.NameScope.Find<ListBox>("PART_Rows");
        if (_rows is not null && Spec is { } spec)
        {
            ListRowBuilder.AttachActions(_rows, spec, () => SelectedItem);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SpecProperty)
        {
            Rebuild();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe(Spec);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Subscribe(null);
    }

    private void Rebuild()
    {
        var spec = Spec;
        Subscribe(spec);
        if (spec is null)
        {
            Title = string.Empty;
            ToolbarItems = [];
            HeaderRow = null;
            RowTemplate = null;
            Rows = [];
            return;
        }

        Region.Mark(this, spec.Title, new RegionInfo(spec.Title, "List", spec.Declared));
        Title = spec.Title;
        ToolbarItems = ListRowBuilder.BuildToolbar(spec, QueueRefresh, () => SelectedItem);
        HeaderRow = ListRowBuilder.BuildHeader(spec);
        RowTemplate = new FuncDataTemplate<object>((item, _) => ListRowBuilder.BuildRow(spec, item), supportsRecycling: true);
        Refresh();
    }

    private void Subscribe(ListSpec? spec)
    {
        if (ReferenceEquals(_subscribed, spec))
        {
            return;
        }

        if (_subscribed is not null)
        {
            _subscribed.Source.Changed -= OnSourceChanged;
        }

        _subscribed = spec;
        if (spec is not null)
        {
            spec.Source.Changed += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, EventArgs e) => QueueRefresh();

    private void QueueRefresh()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
            return;
        }

        if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref _refreshQueued, 0);
                Refresh();
            });
        }
    }
}
