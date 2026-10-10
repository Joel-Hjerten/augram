using Augram.App.Declarations;
using Augram.App.UsedBy;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The Allowed for dialog's edit state (plan 0005 decision 7; Joel, 2026-10-11: from the excluded app's side too, so it is found
/// where the app is): the commands that can work over an excluded app (<see cref="MappingRules.CanWorkOverExcluded"/>: a trigger
/// that holds a button other than the stroke button, not under a hold remap), as a check list (<see cref="CheckListField"/>, the
/// Also in dialog's) labelled "Global › Eyeris › magnifier" with the trigger after it, the ones whose Also in names the app
/// ticked. <see cref="Declare"/> is the form for the shared <c>FormDialog</c>; <see cref="Allowed"/> is what is ticked, which the
/// host stores as one edit (<see cref="MappingStore.SetAllowedFor"/>) when the dialog is confirmed.
/// </summary>
public sealed class AllowedForEditViewModel : ObservableObject
{
    public const string Title = "Allowed for";
    public const string ConfirmLabel = "Save";
    public const string ListLabel = "Commands";
    public const string NoCommandsText = "None can be allowed yet: only a command whose trigger holds a button other than the stroke button (Right + Left, Right + wheel up) works over an excluded app.";
    public const string Help = "Ticked commands still work over this app: only their own buttons are held back there; the stroke button stays the app's. "
        + "The same as ticking the app in each command's Also in.";

    private readonly List<(AppGroup Group, Command Command)> _offered;
    private readonly HashSet<CommandId> _checked;

    /// <param name="appName">The excluded app's name, for the section title.</param>
    /// <param name="offered">The commands that can work over an excluded app, in document order.</param>
    /// <param name="allowed">The commands whose Also in names the app now, ticked.</param>
    /// <param name="names">The platform the triggers are described for.</param>
    public AllowedForEditViewModel(string appName, IEnumerable<(AppGroup Group, Command Command)> offered, IEnumerable<CommandId> allowed, HostPlatform names)
    {
        ArgumentNullException.ThrowIfNull(appName);
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(allowed);
        AppName = appName;
        Names = names;
        _offered = [.. offered];
        _checked = [.. allowed.Where(id => _offered.Exists(pair => pair.Command.Id == id))];
    }

    public string AppName { get; }

    public HostPlatform Names { get; }

    /// <summary>The commands offered, in document order (Global first, then the app groups by name).</summary>
    public IReadOnlyList<(AppGroup Group, Command Command)> Offered => _offered;

    /// <summary>The ticked commands.</summary>
    public IReadOnlyCollection<CommandId> Allowed => _checked;

    /// <summary>The dialog for the excluded app <paramref name="app"/> over <paramref name="mapping"/>, triggers described for <paramref name="platform"/>.</summary>
    public static AllowedForEditViewModel For(IgnoredApp app, MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(mapping);
        var offered = mapping.AllCommands().Where(pair => MappingRules.CanWorkOverExcluded(pair.Command));
        var allowed = mapping.AllCommands().Where(pair => pair.Command.AlsoIn.Contains(app.Id)).Select(pair => pair.Command.Id);
        return new AllowedForEditViewModel(app.Name, offered, allowed, platform);
    }

    public bool IsChecked(CommandId id) => _checked.Contains(id);

    public void SetChecked(CommandId id, bool value)
    {
        if (value ? _checked.Add(id) : _checked.Remove(id))
        {
            OnPropertyChanged(nameof(Allowed));
        }
    }

    public FormScreen Declare() => new(Title,
    [
        new Section($"Commands that still work over '{AppName}'",
        [
            _offered.Count > 0
                ? new CheckListField(ListLabel, [.. _offered.Select(Item)], Help)
                : new NoteField(ListLabel, NoCommandsText, Help),
        ]),
    ]);

    /// <summary>"Global › Eyeris › magnifier", then the trigger ("Right + Left") and "inactive" for one switched off.</summary>
    private CheckListItem Item((AppGroup Group, Command Command) pair)
    {
        var id = pair.Command.Id;
        var trigger = pair.Command.TriggerFor(Names).Describe(Names);
        return new CheckListItem(
            UsedByRow.From(pair.Group, pair.Command).Label,
            new DelegateBinding<bool>(() => IsChecked(id), value => SetChecked(id, value), this, propertyName: nameof(Allowed)),
            pair.Command.IsActive ? trigger : $"{trigger} · inactive");
    }
}
