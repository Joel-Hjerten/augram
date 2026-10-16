using Augram.App.Components.ListEditing;
using Augram.App.Components.MasterDetail;
using Augram.App.Components.SearchBox;
using Augram.App.Dialogs;
using Augram.App.Navigation;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// One Exclusions sub-tab's projection over <see cref="MappingStore"/> (F5 ignore list; plan 0001 M2 step 6; SP.net's Ignore
/// List; Global / Per command, plan 0004; the UI word is "Exclusions" since plan 0005, the code's stays "Ignored"): the entries of
/// its <see cref="Scope"/> as <see cref="MasterItem"/>s sorted by name (name,
/// active, the mode or what uses it, and what the entry matches on this platform), narrowed by the search box (plan 0009:
/// <see cref="SearchText"/>, UI-only, on the name or the summary; a hidden selected entry keeps its form), the selection and the
/// selected app's form (<c>.Panel</c>), and the message line. Turns the <see cref="MasterDetail"/>'s intents into store calls: a new app through
/// the declared form in a dialog, rename in place, the active box, a move to the other list (<c>.PerCommand</c>), delete after
/// a confirmation, undo and redo on the store's one history (the Commands tab's too). The rules live in
/// <see cref="MappingRules"/>; only their messages show here. The engine follows every change through the store
/// (<c>EngineSettingsLink</c>), so an edit here takes effect at once.
/// </summary>
public sealed partial class IgnoredViewModel : StoreViewModel, IDisposable
{
    private readonly MappingStore _store;
    private readonly IFormDialogPresenter _dialogs;
    private readonly HostPlatform _platform;
    private readonly ICommandLocator? _commands;

    /// <summary>
    /// <paramref name="platform"/> is where this runs: the row summary says what an entry matches here. <paramref name="scope"/>
    /// is the sub-tab's list; <paramref name="commands"/> opens a command from a Per command entry's "Used by" (none in tests and
    /// the gallery: the names show as plain text).
    /// </summary>
    public IgnoredViewModel(MappingStore store, IFormDialogPresenter dialogs, IConfirmPresenter confirm, HostPlatform platform, IgnoreScope scope = IgnoreScope.Global, ICommandLocator? commands = null)
        : base(confirm, MappingRefusals)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dialogs);
        _store = store;
        _dialogs = dialogs;
        _platform = platform;
        _commands = commands;
        Scope = scope;
        _store.Changed += OnStoreChanged;
        Project();
    }

    /// <summary>Which list this sub-tab shows: Exclusions › Global, Per command or While holding (plan 0012).</summary>
    public IgnoreScope Scope { get; }

    public bool IsPerCommand => Scope == IgnoreScope.PerCommand;

    /// <summary>"Exclusions › Global", "Exclusions › Per command", "Exclusions › While holding": the sub-tab's screen title.</summary>
    public string ScreenTitle => Scope switch
    {
        IgnoreScope.PerCommand => "Exclusions › Per command",
        IgnoreScope.WhileHolding => "Exclusions › While holding",
        _ => "Exclusions › Global",
    };

    public string Heading => Scope switch
    {
        IgnoreScope.PerCommand => "Per command apps",
        IgnoreScope.WhileHolding => "While holding apps",
        _ => "Excluded apps",
    };

    public string NewLabel => IgnoredEditViewModel.NewTitle(Scope);

    /// <summary>
    /// The right-click menu's move to the other list: between Global and Per command only. A While holding entry moves nowhere
    /// (plan 0012: its inputs exist only on that list, so a move would drop them), and no entry moves onto While holding.
    /// </summary>
    public string? MoveLabel => Scope switch
    {
        IgnoreScope.PerCommand => "Move to Global",
        IgnoreScope.Global => "Move to Per command",
        _ => null,
    };

    /// <summary>The list's ⓘ; on Global it says that hold remaps still work there (plan 0005 decision 7) and what Also in does.</summary>
    public string Help => Scope switch
    {
        IgnoreScope.PerCommand => "Apps listed here change nothing on their own; a command that names one in its Not in does nothing over it and holds no button back there. "
            + "Name them in a command's Not in on the Commands tab (its Change… also adds an app with the magnifier). "
            + "Right-click a row for the menu (Move to Global stops all of Augram over the app); rename with the rename key. Deleting asks first; " + EditKeymap.Current.Undo + " brings it back.",
        IgnoreScope.WhileHolding => "While you hold one of an app's listed keys or buttons, a press over that app passes straight through: no gesture, button trigger or hold remap starts (After Effects navigating with Alt + middle drag). "
            + "Each app keeps a list per platform, as a command's steps; added to the global ignore keys of Options › General. "
            + "Rename with the rename key. Deleting asks first; " + EditKeymap.Current.Undo + " brings it back.",
        _ => "Augram stays out of these apps: over their windows the stroke button passes through untouched, and a \"Disable while focused\" app pauses Augram while it has focus. "
            + IgnoredModes.HoldRemapsInTheseApps + " "
            + "A command whose trigger holds no stroke button (Right + Left, Right + wheel) also works in an app its Also in names. "
            + "Right-click a row for the menu (Move to Per command keeps Augram on and lets single commands leave the app alone); rename with the rename key. Deleting asks first; " + EditKeymap.Current.Undo + " brings it back.",
    };

    public string EmptyDetailText => Scope switch
    {
        IgnoreScope.PerCommand => "Select an app to see how it is recognised and which commands leave it alone, or add one with New app.",
        IgnoreScope.WhileHolding => "Select an app to see how it is recognised and what holding makes gestures pass through, or add one with New app.",
        _ => "Select an excluded app to see how it is recognised, or add one with New excluded app.",
    };

    [ObservableProperty]
    public partial IReadOnlyList<MasterItem> Items { get; private set; } = [];

    [ObservableProperty]
    public partial Guid? SelectedId { get; private set; }

    /// <summary>The search box's text (plan 0009): entries whose name or summary matches (<see cref="SearchQuery"/>); never persisted, cleared when the main window hides.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public void Handle(MasterDetailActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        Guard(() => Dispatch(e));
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        Panel.Close();
    }

    partial void OnSearchTextChanged(string value) => Project();

    private void Dispatch(MasterDetailActionEventArgs e)
    {
        switch (e.Action)
        {
            case MasterDetailAction.Select:
                SelectedId = e.Item?.Id;
                ProjectDetail();
                break;
            case MasterDetailAction.New:
                _ = NewAsync();
                break;
            case MasterDetailAction.Rename when e.Item is { } item && e.Name is { } name:
                _store.UpdateIgnored(Require(item.Id) with { Name = name });
                break;
            case MasterDetailAction.ToggleActive when e.Item is { } item:
                var app = Require(item.Id);
                _store.UpdateIgnored(app with { IsActive = !app.IsActive });
                break;
            case MasterDetailAction.Move when e.Item is { } item && MoveLabel is not null:
                _ = MoveAsync(item);
                break;
            case MasterDetailAction.Delete when e.Item is { } item:
                _ = DeleteAsync(item);
                break;
            case MasterDetailAction.Undo:
                _store.Undo();
                break;
            case MasterDetailAction.Redo:
                _store.Redo();
                break;
        }
    }

    private async Task NewAsync()
    {
        var edit = new IgnoredEditViewModel(Scope);
        if (!await _dialogs.ShowAsync(edit).ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var stored = _store.AddIgnored(edit.ToIgnored(GroupId.New()));
            SelectedId = stored.Id.Value;
            ProjectDetail();
        });
    }

    private async Task DeleteAsync(MasterItem item)
    {
        var (title, question) = Scope switch
        {
            IgnoreScope.PerCommand => ("Delete app", $"Delete '{item.Name}' from Per command?{UsersSentence(new GroupId(item.Id))}"),
            IgnoreScope.WhileHolding => ("Delete app", $"Delete '{item.Name}' from While holding? Holding its keys and buttons no longer makes gestures pass through over it."),
            _ => ("Delete excluded app", $"Delete excluded app '{item.Name}'? Augram works over it again."),
        };
        await DeleteAsync(title, question, () => Deleted(_store.RemoveIgnored(new GroupId(item.Id)).Name, EditKeymap.Current.Undo)).ConfigureAwait(true);
    }

    protected override void OnStoreChangedOnUi() => Project();

    private void Project()
    {
        var search = SearchQuery.Parse(SearchText);
        Items = [.. _store.Current.Ignored
            .Where(app => app.Scope == Scope)
            .OrderBy(app => app.Name, MappingRules.NameComparer)
            .ThenBy(app => app.Id.Value)
            .Select(app => new MasterItem(app.Id.Value, app.Name, app.IsActive, Summary(app)))
            .Where(item => search.Matches(item.Name, item.Summary))];
        // Gone, or moved to the other list.
        if (SelectedId is { } id && Find(id)?.Scope != Scope)
        {
            SelectedId = null;
        }

        ProjectDetail();
    }

    /// <summary>
    /// "Gestures off over this app · blender.exe", "Used by 2 commands · Spine.exe", "While holding Alt, S · AfterFX.exe": the mode
    /// (its users, or what holding passes through here) and what it matches here.
    /// </summary>
    private string Summary(IgnoredApp app)
    {
        var first = app.Scope switch
        {
            IgnoreScope.PerCommand => UsersText(app.Id),
            IgnoreScope.WhileHolding => app.HeldInputsOn(_platform) is { Count: > 0 } inputs
                ? $"While holding {string.Join(", ", inputs.Select(input => input.Describe(_platform)))}"
                : "Nothing to hold yet",
            _ => IgnoredModes.Label(app.DisableEntirely),
        };
        return $"{first} · {app.Matcher.Summary(_platform).Text}";
    }

    private IgnoredApp? Find(Guid id) => _store.FindIgnored(new GroupId(id));

    private IgnoredApp Require(Guid id) => Find(id) ?? throw new KeyNotFoundException("That excluded app no longer exists.");
}
