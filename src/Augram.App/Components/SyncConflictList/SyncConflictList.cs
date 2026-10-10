using Augram.App.Inspector;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.SyncConflictList;

/// <summary>
/// Lookless list of pending sync conflicts (F8 sync): a column header (item, this machine, the other machine,
/// choice) over one <see cref="SyncConflictRow"/> per entry of <see cref="Entries"/>. Presentational: entries in,
/// <see cref="ChoiceChanged"/> out with the entry's index; the conflict dialog's view model keeps the choices. The two
/// versions' column titles are <see cref="MineHeader"/> and <see cref="TheirsHeader"/> ("Yours" and "In the file" in the
/// Augram file import, plan 0003).
/// </summary>
public sealed class SyncConflictList : TemplatedControl
{
    public const string ThisMachineHeader = "This machine";
    public const string OtherMachineHeader = "Other machine";

    public static readonly StyledProperty<IReadOnlyList<SyncConflictEntry>> EntriesProperty =
        AvaloniaProperty.Register<SyncConflictList, IReadOnlyList<SyncConflictEntry>>(nameof(Entries), []);

    public static readonly StyledProperty<string> MineHeaderProperty =
        AvaloniaProperty.Register<SyncConflictList, string>(nameof(MineHeader), ThisMachineHeader);

    public static readonly StyledProperty<string> TheirsHeaderProperty =
        AvaloniaProperty.Register<SyncConflictList, string>(nameof(TheirsHeader), OtherMachineHeader);

    public static readonly StyledProperty<IReadOnlyList<Control>> RowsProperty =
        AvaloniaProperty.Register<SyncConflictList, IReadOnlyList<Control>>(nameof(Rows), []);

    public event EventHandler<SyncConflictChoiceEventArgs>? ChoiceChanged;

    public IReadOnlyList<SyncConflictEntry> Entries
    {
        get => GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    /// <summary>The title over this side's versions: "This machine" unless the host says otherwise.</summary>
    public string MineHeader
    {
        get => GetValue(MineHeaderProperty);
        set => SetValue(MineHeaderProperty, value);
    }

    /// <summary>The title over the other side's versions: "Other machine" unless the host says otherwise.</summary>
    public string TheirsHeader
    {
        get => GetValue(TheirsHeaderProperty);
        set => SetValue(TheirsHeaderProperty, value);
    }

    /// <summary>The built rows, one per entry, in order; the template lists them.</summary>
    public IReadOnlyList<Control> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EntriesProperty)
        {
            Rows = [.. Entries.Select(BuildRow)];
        }
    }

    private SyncConflictRow BuildRow(SyncConflictEntry entry, int index)
    {
        var row = new SyncConflictRow { Entry = entry };
        row.ChoiceChanged += (_, choice) => ChoiceChanged?.Invoke(this, new SyncConflictChoiceEventArgs(index, choice));
        Region.Mark(row, entry.Name);
        return row;
    }
}
