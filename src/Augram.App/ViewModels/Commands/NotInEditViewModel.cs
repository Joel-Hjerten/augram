using Augram.App.Components.CommandTree;
using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Not in dialog's edit state (Joel, 2026-10-10, plan 0004 decisions 6 and 7): a command's "not used over these apps" as a
/// check list (<see cref="CheckListField"/>, the export dialog's) of the Exclusions › Per command entries by name, the ones it
/// names ticked, and <see cref="AddLabel"/> with the window finder's magnifier: the picked window's app becomes a new Per command
/// entry (<see cref="AddApp"/>), listed and ticked at once. <see cref="Declare"/> is the form for the shared <c>FormDialog</c>;
/// <see cref="NotIn"/> is what is ticked and <see cref="Added"/> the entries Add app… made, which the host stores together as one
/// edit when the dialog is confirmed (Cancel adds nothing). What a list may hold is Core's (<see cref="MappingRules"/> keeps only
/// Per command entries that exist); this only collects the ticks.
/// </summary>
public sealed partial class NotInEditViewModel : ObservableObject
{
    public const string Title = "Not in";
    public const string ConfirmLabel = "Save";
    public const string ListLabel = "Apps";
    public const string NoAppsText = "None on Exclusions › Per command yet: add one with the magnifier below.";
    public const string AddLabel = "Add app…";
    public const string AddPrompt = "Drag the magnifier onto the app's window.";
    public const string AddHelp = "The window's app joins Exclusions › Per command, named after its executable and matched on it (as the identification form's "
        + "Executable magnifier fills it), and is ticked here. Save stores it with the ticks; Cancel adds nothing. An app already on Per command is ticked instead.";

    private readonly IReadOnlyList<IgnoredApp> _ignored;
    private readonly List<IgnoredApp> _entries;
    private readonly List<IgnoredApp> _added = [];
    private readonly HashSet<GroupId> _checked;
    private readonly HostPlatform _platform;

    /// <param name="commandName">The command's name, for the section title.</param>
    /// <param name="ignored">The whole ignore list: its Per command entries are offered, and a new entry's name is free among all of it.</param>
    /// <param name="notIn">The entries the command names now, ticked.</param>
    /// <param name="platform">Where this runs: a picked window is one of its windows.</param>
    public NotInEditViewModel(string commandName, IEnumerable<IgnoredApp> ignored, IEnumerable<GroupId> notIn, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(ignored);
        ArgumentNullException.ThrowIfNull(notIn);
        CommandName = commandName;
        _platform = platform;
        _ignored = [.. ignored];
        _entries = [.. _ignored.Where(app => app.IsPerCommand)];
        _checked = [.. notIn.Where(id => _entries.Exists(app => app.Id == id))];
    }

    public string CommandName { get; }

    /// <summary>The Per command entries offered, by name, the ones Add app… made among them.</summary>
    public IReadOnlyList<IgnoredApp> Entries => [.. _entries.OrderBy(app => app.Name, MappingRules.NameComparer).ThenBy(app => app.Id.Value)];

    /// <summary>The new Per command entries Add app… made, in the order made; Save stores them with <see cref="NotIn"/>.</summary>
    public IReadOnlyList<IgnoredApp> Added => _added;

    /// <summary>The ticked entries, in the stored order (by id).</summary>
    public IReadOnlyList<GroupId> NotIn => [.. _checked.OrderBy(id => id.Value)];

    /// <summary>What the last Add app… did ("Added 'Spine' (Spine.exe), ticked."); null before the first.</summary>
    [ObservableProperty]
    public partial string? AddStatus { get; private set; }

    /// <summary>The dialog for <paramref name="command"/> over <paramref name="mapping"/>'s ignore list.</summary>
    public static NotInEditViewModel For(Command command, MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(mapping);
        return new NotInEditViewModel(command.Name, mapping.Ignored, command.NotIn, platform);
    }

    public bool IsChecked(GroupId id) => _checked.Contains(id);

    public void SetChecked(GroupId id, bool value)
    {
        if (value ? _checked.Add(id) : _checked.Remove(id))
        {
            OnPropertyChanged(nameof(NotIn));
        }
    }

    /// <summary>
    /// The window finder's pick: a Per command entry that already claims the window is ticked; otherwise a new one, named after
    /// the executable (<see cref="AppMatcherEditViewModel.AppNameOf"/>, made free among the ignore list's names: "Spine 2") and
    /// matching it as the Executable row's magnifier fills that row (<see cref="AppMatcherEditViewModel.TakeExecutable"/>), joins
    /// the list ticked. Returns the entry ticked.
    /// </summary>
    public IgnoredApp AddApp(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_entries.Find(app => app.Matcher.Matches(window, _platform)) is { } existing)
        {
            SetChecked(existing.Id, true);
            AddStatus = $"'{existing.Name}' is on Per command already: ticked.";
            return existing;
        }

        var identification = new AppMatcherEditViewModel(() => PlatformSet.All, string.Empty, string.Empty) { Platform = _platform };
        identification.TakeExecutable(window);
        var name = NameScope.Free(AppMatcherEditViewModel.AppNameOf(window), _ignored.Concat(_added).Select(app => app.Name));
        var added = new IgnoredApp(GroupId.New(), name, IsActive: true, identification.ToMatcher(), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
        _added.Add(added);
        _entries.Add(added);
        OnPropertyChanged(nameof(Entries));
        SetChecked(added.Id, true);

        var global = _ignored.FirstOrDefault(app => !app.IsPerCommand && app.IsActive && app.Matcher.Matches(window, _platform));
        AddStatus = $"Added '{name}' ({window.ProcessName}), ticked."
            + (global is null ? string.Empty : $" '{global.Name}' on Exclusions › Global stops all of Augram over it already.");
        return added;
    }

    public FormScreen Declare() => new(Title,
    [
        new Section($"Where '{CommandName}' is not used",
        [
            new CheckListField(ListLabel, CheckItems(), CommandHeader.NotInHelp)
            {
                LiveItems = new DelegateBinding<IReadOnlyList<CheckListItem>>(CheckItems, owner: this, propertyName: nameof(Entries)),
                Visible = new DelegateBinding<bool>(() => _entries.Count > 0, owner: this, propertyName: nameof(Entries)),
            },
            new NoteField(ListLabel, NoAppsText, CommandHeader.NotInHelp)
            {
                Visible = new DelegateBinding<bool>(() => _entries.Count == 0, owner: this, propertyName: nameof(Entries)),
            },
            new NoteField(AddLabel, new DelegateBinding<string>(() => AddStatus ?? AddPrompt, owner: this, propertyName: nameof(AddStatus)), AddHelp)
            {
                Accessory = WindowFinderAccessory.Finder(WindowFinder.Summary, window => AddApp(window)),
            },
        ]),
    ]);

    /// <summary>"inactive", "new" (made by Add app…, stored on Save), both, or no detail.</summary>
    private string? Detail(IgnoredApp app)
    {
        var parts = new List<string>(2);
        if (!app.IsActive)
        {
            parts.Add("inactive");
        }

        if (_added.Contains(app))
        {
            parts.Add("new");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private IReadOnlyList<CheckListItem> CheckItems() => [.. Entries.Select(Item)];

    private CheckListItem Item(IgnoredApp app)
        => new(app.Name, new DelegateBinding<bool>(() => IsChecked(app.Id), value => SetChecked(app.Id, value), this, propertyName: nameof(NotIn)), Detail(app));
}
