using System.Globalization;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Transfer;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The export dialog (requirements F8; plan 0003 step 3) as a declared form for <c>FormDialog</c>: the scope (Everything,
/// Gestures only, Selected), under Selected a check list of Global, the app groups by name and the ignored apps, what the
/// file will hold (<see cref="TransferContents"/>), and, when some steps type text or run command lines, the note that they
/// travel as written. What a scope writes is Core's (<see cref="Exporter"/>); this only collects the choice. It works on one
/// snapshot of the configuration (<see cref="ConfigSession.Document"/>), the one <see cref="Export"/> writes from, and starts
/// on the scope its entry point preselects (Options: everything; the Gestures toolbar: gestures only; an app group's or a
/// category's menu: that group, or Global).
/// </summary>
public sealed partial class ExportViewModel : ObservableObject
{
    public const string Title = "Export";
    public const string ConfirmLabel = "Save…";
    public const string NothingSelectedText = "Nothing selected yet: tick an app group or an ignored app.";
    public const string ScopeHelp =
        "Everything: the options (not Sync), every gesture and every command. Gestures only: the gesture library. Selected: the app groups and ignored apps ticked below, with the gestures their commands use.";
    public const string SelectionHelp = "A group goes whole, with its categories, hold remaps and commands. Global holds the global commands.";
    public const string ContentsHelp = "The file is Augram's own format (.augram.json): Import… on any Augram reads it, merging rather than replacing.";

    private readonly ConfigDocument _current;
    private readonly HashSet<GroupId> _checked = [];

    /// <param name="current">The configuration to export from: the stores' snapshot.</param>
    /// <param name="start">The scope to start on; a selection's groups and ignored apps start ticked.</param>
    public ExportViewModel(ConfigDocument current, ExportScope start)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(start);
        _current = current;
        Items = [.. GroupItems(current.Mapping), .. IgnoredItems(current.Mapping)];
        switch (start)
        {
            case ExportScope.Selection selection:
                _checked.UnionWith(Items.Where(item => (item.IsIgnoredApp ? selection.Ignored : selection.Groups).Contains(item.Id)).Select(item => item.Id));
                Kind = ExportKind.Selected;
                break;
            case ExportScope.GesturesScope:
                Kind = ExportKind.GesturesOnly;
                break;
            default:
                Kind = ExportKind.Everything;
                break;
        }

        Contents = TransferContents.Of(Export());
    }

    public static IReadOnlyList<Choice<ExportKind>> Kinds { get; } =
    [
        new("Everything", ExportKind.Everything),
        new("Gestures only", ExportKind.GesturesOnly),
        new("Selected", ExportKind.Selected),
    ];

    [ObservableProperty]
    public partial ExportKind Kind { get; set; }

    /// <summary>Global first, then the app groups by name, then the ignored apps by name.</summary>
    public IReadOnlyList<ExportItem> Items { get; }

    /// <summary>What the file will hold under the current choice.</summary>
    public TransferContents Contents { get; private set; }

    /// <summary>"options, 90 gestures, Global, 19 app groups, 212 commands, 3 ignored apps", or what to do when nothing is selected.</summary>
    public string ContentsText => CanSave ? Contents.ToString() : NothingSelectedText;

    public bool HasPrivateText => CanSave && Contents.PrivateTextSteps > 0;

    /// <summary>"3 steps type text or run command lines; they are in the file as written. Do not share it if they hold passwords." (decision 18)</summary>
    public string PrivateTextNote => PrivateText(Contents.PrivateTextSteps);

    /// <summary>Something to export: always, except a selection with nothing ticked.</summary>
    public bool CanSave => Kind != ExportKind.Selected || _checked.Count > 0;

    /// <summary>The scope the choice stands for, as Core takes it.</summary>
    public ExportScope Scope => Kind switch
    {
        ExportKind.GesturesOnly => ExportScope.GesturesOnly,
        ExportKind.Selected => ExportScope.Of(
            Items.Where(item => !item.IsIgnoredApp && _checked.Contains(item.Id)).Select(item => item.Id),
            Items.Where(item => item.IsIgnoredApp && _checked.Contains(item.Id)).Select(item => item.Id)),
        _ => ExportScope.Everything,
    };

    public static string PrivateText(int steps) => steps == 1
        ? "1 step types text or runs a command line; it is in the file as written. Do not share it if it holds a password."
        : string.Create(CultureInfo.InvariantCulture, $"{steps} steps type text or run command lines; they are in the file as written. Do not share it if they hold passwords.");

    public bool IsChecked(ExportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _checked.Contains(item.Id);
    }

    public void SetChecked(ExportItem item, bool value)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (value ? _checked.Add(item.Id) : _checked.Remove(item.Id))
        {
            Refresh();
        }
    }

    /// <summary>The file the current choice writes, from the snapshot the dialog opened on.</summary>
    public TransferFile Export() => Exporter.Export(Scope, _current);

    /// <summary>"Augram Blender 2026-10-10.augram.json" (<see cref="Exporter.SuggestedFileName"/>).</summary>
    public string SuggestedFileName(DateOnly today) => Exporter.SuggestedFileName(Scope, _current.Mapping, today);

    public FormScreen Declare() => new(Title,
    [
        new Section("What to export",
        [
            new ButtonRadioField<ExportKind>("Scope", Kinds, new DelegateBinding<ExportKind>(() => Kind, value => Kind = value, this), ScopeHelp),
            new CheckListField("App groups", [.. Items.Select(Item)], SelectionHelp)
            {
                Visible = new DelegateBinding<bool>(() => Kind == ExportKind.Selected, owner: this, propertyName: nameof(Kind)),
            },
            new NoteField("In the file", new DelegateBinding<string>(() => ContentsText, owner: this), ContentsHelp),
            new NoteField("Typed text", new DelegateBinding<string>(() => PrivateTextNote, owner: this))
            {
                Visible = new DelegateBinding<bool>(() => HasPrivateText, owner: this, propertyName: nameof(HasPrivateText)),
            },
        ]),
    ]);

    partial void OnKindChanged(ExportKind value) => Refresh();

    private CheckListItem Item(ExportItem item)
        => new(item.Name, new DelegateBinding<bool>(() => IsChecked(item), value => SetChecked(item, value), this, propertyName: nameof(Contents)), item.Detail);

    /// <summary>Recounts the file and tells every binding on the form (an empty name refreshes them all).</summary>
    private void Refresh()
    {
        Contents = TransferContents.Of(Export());
        OnPropertyChanged(string.Empty);
    }

    private static IEnumerable<ExportItem> GroupItems(MappingDocument mapping)
        => mapping.Groups
            .OrderBy(group => group.IsGlobal ? 0 : 1)
            .ThenBy(group => group.Name, MappingRules.NameComparer)
            .Select(group => new ExportItem(group.Id, IsIgnoredApp: false, group.Name, GroupDetail(group)));

    private static IEnumerable<ExportItem> IgnoredItems(MappingDocument mapping)
        => mapping.Ignored
            .OrderBy(app => app.Name, MappingRules.NameComparer)
            .Select(app => new ExportItem(app.Id, IsIgnoredApp: true, app.Name, "ignored app"));

    /// <summary>"8 commands · 1 hold remap · Windows only".</summary>
    private static string GroupDetail(AppGroup group)
    {
        var parts = new List<string> { Count(group.Commands.Count, "command") };
        if (group.HoldRemaps.Count > 0)
        {
            parts.Add(Count(group.HoldRemaps.Count, "hold remap"));
        }

        if (!group.IsGlobal && group.UseOn != PlatformSet.All)
        {
            parts.Add(StepPlatformMarker.Only(group.UseOn));
        }

        return string.Join(" · ", parts);
    }

    private static string Count(int count, string noun)
        => string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
}
