using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.MasterDetail;
using Augram.App.UsedBy;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The Ignored tab's projection over <see cref="MappingStore"/> (F5 ignore list; plan 0001 M2 step 6; SP.net's Ignore List):
/// the ignored apps as <see cref="MasterItem"/>s sorted by name (name, active, the mode and what the entry matches on this
/// platform), the selection and the selected app's form (<c>.Panel</c>), and the message line. Turns the
/// <see cref="MasterDetail"/>'s intents into store calls: a new app through the declared form in a dialog, rename in place,
/// the active box, delete after a confirmation, undo and redo on the store's one history (the Commands tab's too). The
/// rules live in <see cref="MappingRules"/>; only their messages show here. The engine follows every change through the
/// store (<c>EngineSettingsLink</c>), so an edit here takes effect at once.
/// </summary>
public sealed partial class IgnoredViewModel : ObservableObject, IDisposable
{
    private readonly MappingStore _store;
    private readonly IFormDialogPresenter _dialogs;
    private readonly IConfirmPresenter _confirm;
    private readonly HostPlatform _platform;

    /// <summary><paramref name="platform"/> is where this runs: the row summary says what an entry matches here.</summary>
    public IgnoredViewModel(MappingStore store, IFormDialogPresenter dialogs, IConfirmPresenter confirm, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(confirm);
        _store = store;
        _dialogs = dialogs;
        _confirm = confirm;
        _platform = platform;
        _store.Changed += OnStoreChanged;
        Project();
    }

    public string Heading => "Ignored apps";

    public string NewLabel => "New ignored app";

    public string Help =>
        "Augram stays out of these apps: over their windows the stroke button passes through untouched, and a \"Disable while focused\" app pauses Augram while it has focus. "
        + "Right-click a row for the menu; rename with the rename key. Deleting asks first; " + CommandsKeymap.Current.Undo + " brings it back.";

    public string EmptyDetailText => "Select an ignored app to see how it is recognised, or add one with New ignored app.";

    [ObservableProperty]
    public partial IReadOnlyList<MasterItem> Items { get; private set; } = [];

    [ObservableProperty]
    public partial Guid? SelectedId { get; private set; }

    /// <summary>Feedback for the last action: a rule message, or "Deleted 'X'. Ctrl+Z undoes it."</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    public void Handle(MasterDetailActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        Guard(() => Dispatch(e));
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        DetachEdit();
    }

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
        var edit = new IgnoredEditViewModel();
        if (!await _dialogs.ShowAsync(new FormDialogRequest("New ignored app", "Create", Screen: edit.Declare())).ConfigureAwait(true))
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
        if (!await _confirm.ConfirmAsync("Delete ignored app", $"Delete ignored app '{item.Name}'? Augram works over it again.", "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var removed = _store.RemoveIgnored(new GroupId(item.Id));
            Message = $"Deleted '{removed.Name}'. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Project();
        }
        else
        {
            Dispatcher.UIThread.Post(Project);
        }
    }

    private void Project()
    {
        Items = [.. _store.Current.Ignored
            .OrderBy(app => app.Name, MappingRules.NameComparer)
            .ThenBy(app => app.Id.Value)
            .Select(app => new MasterItem(app.Id.Value, app.Name, app.IsActive, Summary(app)))];
        if (SelectedId is { } id && _store.FindIgnored(new GroupId(id)) is null)
        {
            SelectedId = null;
        }

        ProjectDetail();
    }

    /// <summary>"Gestures off over this app · blender.exe", "Disable while focused · nothing on macOS": the mode and what it matches here.</summary>
    private string Summary(IgnoredApp app) => $"{IgnoredModes.Label(app.DisableEntirely)} · {Matches(app.Matcher)}";

    private string Matches(AppMatcher matcher)
    {
        if (matcher.IsEmpty)
        {
            return "matches nothing yet";
        }

        if (!IgnoreList.CanMatchOn(matcher, _platform))
        {
            return "nothing on " + (_platform == HostPlatform.MacOS ? "macOS" : "Windows");
        }

        if (matcher.HasProcessNames)
        {
            var names = string.Join(", ", matcher.EffectiveProcessNames(_platform));
            return matcher.IsGuessedOn(_platform) ? names + " (guessed)" : names;
        }

        return matcher.Title ?? matcher.ProcessPath ?? "window classes";
    }

    private IgnoredApp Require(Guid id) => _store.FindIgnored(new GroupId(id)) ?? throw new KeyNotFoundException("That ignored app no longer exists.");

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (MappingValidationException exception)
        {
            Message = exception.Message;
        }
        catch (KeyNotFoundException exception)
        {
            Message = exception.Message;
        }
    }
}
