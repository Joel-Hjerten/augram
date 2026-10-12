using System.Runtime.CompilerServices;
using Augram.App.Declarations;
using Augram.App.ViewModels;

namespace Augram.App.Screens;

/// <summary>
/// The Sync section of Options › Sync (F8 sync), declared like the rest of the page: repository address, this
/// machine's name, automatic sync, Sync now with the status line, the conflicts line with Resolve… (shown only while
/// conflicts are pending) and the last run's details. The two text fields commit on leaving or Enter, not per
/// keystroke (<see cref="DraftText"/>, a <see cref="CommittedTextField"/>).
/// </summary>
public static class OptionsSyncSection
{
    public const string Title = "Sync";

    public const string RepositoryHelp =
        "A private GitHub repository, e.g. https://github.com/you/augram-settings. Sign-in is git's own (Git Credential Manager on Windows, the Keychain on macOS); Augram stores no password. Clear it to turn sync off.";

    public const string SectionHelp =
        "Gestures and commands follow you between machines through a private git repository; everything else on this page stays on each machine.";

    public static Section Declare(SyncViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new Section(Title,
        [
            Committed("Repository", vm.RepositoryUrl, RepositoryHelp) with { Wide = true },
            Committed("This machine", vm.MachineName, "How the other machines name this one (\"3 commands changed from PC-HOME\")."),
            new ToggleField("Automatic sync",
                new DelegateBinding<bool>(() => vm.AutoSync, value => vm.AutoSync = value, vm),
                "At start, 20 seconds after a change and every 5 minutes. Off: only Sync now."),
            new ButtonsField("Status", [new FieldButton("Sync now", vm.SyncNow, new DelegateBinding<bool>(() => vm.CanSyncNow, owner: vm, propertyName: nameof(vm.CanSyncNow)))])
            {
                Status = new DelegateBinding<string>(() => vm.StatusText, owner: vm, propertyName: nameof(vm.StatusText)),
            },
            new ButtonsField(
                "Conflicts",
                [new FieldButton("Resolve…", () => _ = vm.ResolveConflictsAsync(), new DelegateBinding<bool>(() => vm.HasConflicts, owner: vm, propertyName: nameof(vm.HasConflicts)))],
                "Changed here and on another machine. Until you choose, this machine keeps its version; nothing blocks.")
            {
                Status = new DelegateBinding<string>(() => vm.ConflictsText, owner: vm, propertyName: nameof(vm.ConflictsText)),
                Visible = new DelegateBinding<bool>(() => vm.HasConflicts, owner: vm, propertyName: nameof(vm.HasConflicts)),
            },
            new NoteField("Details", new DelegateBinding<string>(() => vm.DetailsText, owner: vm), "What the last sync renamed or skipped.")
            {
                Visible = new DelegateBinding<bool>(() => vm.HasDetails, owner: vm, propertyName: nameof(vm.HasDetails)),
            },
        ], SectionHelp);
    }

    /// <summary>A text field over a <see cref="DraftText"/>: its draft, its problem, commit and revert; F1 points at the line that declares it.</summary>
    private static CommittedTextField Committed(string label, DraftText draft, string help, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => new(
        label,
        new DelegateBinding<string>(() => draft.Text, value => draft.Text = value, draft),
        new DelegateBinding<string>(() => draft.ProblemText, owner: draft),
        draft.Commit,
        draft.Revert,
        help,
        file,
        line);
}
