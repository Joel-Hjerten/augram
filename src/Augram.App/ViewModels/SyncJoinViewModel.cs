using System.Globalization;
using Augram.App.Declarations;
using Augram.Core.Sync;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The join question (F8 sync, join) as a declared form for <c>FormDialog</c>: the machines already in the repository
/// (name, when their file was last written, gesture, group and command counts) and the choice, "Use the synced
/// settings on this machine" (the default, recommended) or "Merge with this machine's settings", with a line saying
/// what the selected one does. <see cref="Choice"/> is the answer when the dialog confirms; Cancel answers nothing.
/// </summary>
public sealed partial class SyncJoinViewModel : ObservableObject
{
    public const string Title = "Join sync";
    public const string ConfirmLabel = "Join";
    public const string UseRemoteLabel = "Use the synced settings on this machine (recommended)";
    public const string MergeLabel = "Merge with this machine's settings";
    public const string Message =
        "This repository already holds Augram settings from another machine. Choose how this machine joins; nothing changes until you press Join. Cancel keeps sync paused.";

    public SyncJoinViewModel(IReadOnlyList<SyncMachineSummary> machines)
    {
        ArgumentNullException.ThrowIfNull(machines);
        Machines = [.. machines.OrderByDescending(machine => machine.WrittenAt)];
    }

    public static IReadOnlyList<Choice<SyncJoin>> Choices { get; } =
    [
        new(UseRemoteLabel, SyncJoin.UseRemote),
        new(MergeLabel, SyncJoin.Merge),
    ];

    /// <summary>Newest first: the first is the one "use the synced settings" adopts.</summary>
    public IReadOnlyList<SyncMachineSummary> Machines { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Explanation))]
    public partial SyncJoin Choice { get; set; } = SyncJoin.UseRemote;

    /// <summary>What the selected choice does to this machine.</summary>
    public string Explanation => Choice == SyncJoin.UseRemote
        ? $"This machine's gestures and commands are replaced by {Newest}'s. The previous file is kept as a backup."
        : "Keeps both sets. Items imported separately on each machine (from StrokesPlus.net, say) will appear twice.";

    private string Newest => Machines.Count > 0 ? Machines[0].MachineName : "the other machine";

    /// <summary>"Last written 7 Oct 09:12 · 105 gestures · 4 groups · 212 commands", in local time.</summary>
    public static string Describe(SyncMachineSummary machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var written = machine.WrittenAt.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"Last written {written} · {machine.Gestures} gestures · {machine.Groups} groups · {machine.Commands} commands");
    }

    public FormScreen Declare() => new(Title,
    [
        new Section("Already in the repository",
            [.. Machines.Select(machine => (Field)new NoteField(machine.MachineName, Describe(machine)))]),
        new Section("This machine",
        [
            new DropdownField<SyncJoin>("Join by", Choices, new DelegateBinding<SyncJoin>(() => Choice, value => Choice = value, this)),
            new NoteField("What happens", new DelegateBinding<string>(() => Explanation, owner: this)),
        ]),
    ]);
}
