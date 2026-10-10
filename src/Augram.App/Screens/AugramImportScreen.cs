using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Sync;
using Avalonia.Controls;

namespace Augram.App.Screens;

/// <summary>
/// The top of the import review (plan 0003 step 4), declared like any form: under the file's name (with the import's help in
/// its ⓘ) what the file holds, how it compares with this configuration, "Everything in this file is already here." when there
/// is nothing to import, the items matched by name or shape, "Also use its options" when the file has options, "Apply to all"
/// when items differ, what the result would need under the current choices, the file's notices, and why an import failed.
/// The differing items' list and the buttons sit below it (<c>Transfer/AugramImportView</c>).
/// </summary>
public static class AugramImportScreen
{
    public const string ApplyToAllLabel = "Apply to all";

    public const string MatchedHelp =
        "An item whose id is not here is matched by name (a hold remap also by its hold key, a gesture also by its shape), so a file from another machine lines up with yours instead of arriving twice.";

    public const string OutcomeHelp =
        "What the result needs so it passes the rules: an item renamed because its name is taken, a trigger left unbound because another command of its group uses it. Changes with the choices.";

    /// <summary>The choices "Apply to all" offers, in the dropdown's order.</summary>
    public static IReadOnlyList<SyncChoice> AllChoices { get; } = [SyncChoice.KeepMine, SyncChoice.TakeTheirs, SyncChoice.KeepBoth];

    public static FormScreen Declare(AugramImportViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new FormScreen(AugramImportViewModel.Title,
        [
            new Section(vm.FileName,
            [
                new NoteField("In the file", new DelegateBinding<string>(() => vm.ContentsText, owner: vm)),
                new NoteField("Compared with yours", new DelegateBinding<string>(() => vm.CountsText, owner: vm)),
                new NoteField("Nothing to import", AugramImportViewModel.EmptyText)
                {
                    Visible = new DelegateBinding<bool>(() => vm.IsEmpty, owner: vm, propertyName: nameof(vm.IsEmpty)),
                },
                new NoteField("Matched", new DelegateBinding<string>(() => vm.MatchesText, owner: vm), MatchedHelp)
                {
                    Visible = new DelegateBinding<bool>(() => vm.HasMatches, owner: vm, propertyName: nameof(vm.HasMatches)),
                },
                new ToggleField("Also use its options", new DelegateBinding<bool>(() => vm.TakeSettings, value => vm.TakeSettings = value, vm), AugramImportViewModel.OptionsHelp)
                {
                    Visible = new DelegateBinding<bool>(() => vm.HasSettings, owner: vm, propertyName: nameof(vm.HasSettings)),
                },
                new CustomField("Every differing item", () => ApplyToAll(vm), vm)
                {
                    Visible = new DelegateBinding<bool>(() => vm.HasConflicts, owner: vm, propertyName: nameof(vm.HasConflicts)),
                },
                new NoteField("After the import", new DelegateBinding<string>(() => vm.OutcomeText, owner: vm), OutcomeHelp)
                {
                    Visible = new DelegateBinding<bool>(() => vm.HasOutcome, owner: vm, propertyName: nameof(vm.HasOutcome)),
                },
                new NoteField("File notices", new DelegateBinding<string>(() => vm.NoticesText, owner: vm))
                {
                    Visible = new DelegateBinding<bool>(() => vm.HasNotices, owner: vm, propertyName: nameof(vm.HasNotices)),
                },
                new NoteField("Not imported", new DelegateBinding<string>(() => vm.Error, owner: vm))
                {
                    Visible = new DelegateBinding<bool>(() => vm.HasError, owner: vm, propertyName: nameof(vm.HasError)),
                },
            ], vm.Conflicts.Help),
        ]);
    }

    /// <summary>A dropdown of the three choices and "Apply to all" beside it, as the StrokesPlus.net import has.</summary>
    private static StackPanel ApplyToAll(AugramImportViewModel vm)
    {
        var choice = new ComboBox { ItemsSource = AllChoices.Select(SyncConflictsViewModel.Label).ToList(), SelectedIndex = 0 };
        choice.Classes.Add("field-editor");
        var apply = new Button { Content = ApplyToAllLabel };
        apply.Classes.Add("toolbar");
        apply.Click += (_, _) => vm.ApplyToAll(AllChoices[Math.Max(0, choice.SelectedIndex)]);
        var line = new StackPanel { Children = { choice, apply } };
        line.Classes.Add("field-buttons");
        return line;
    }
}
