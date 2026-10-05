using Augram.App.Declarations;
using Augram.Core.Gestures;
using Augram.Import.StrokesPlus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Import;

/// <summary>One name clash in the merge plan (F8) and the user's <see cref="MergeChoice"/> for it; default KeepMine.</summary>
public sealed partial class ImportConflictItem : ObservableObject
{
    public static IReadOnlyList<Choice<MergeChoice>> Choices { get; } =
    [
        new("Keep mine", MergeChoice.KeepMine),
        new("Take theirs", MergeChoice.TakeTheirs),
        new("Keep both", MergeChoice.KeepBoth),
    ];

    public ImportConflictItem(MergeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Existing is null)
        {
            throw new ArgumentException("Only conflict entries have an existing gesture.", nameof(entry));
        }

        ImportedId = entry.Imported.Id;
        Name = entry.Imported.Name;
        Summary = $"theirs: {entry.Imported.Samples.Count} sample(s), {(entry.Imported.IsActive ? "active" : "inactive")} · mine: {entry.Existing.Samples.Count} sample(s), {(entry.Existing.IsActive ? "active" : "inactive")}";
        SelectedChoice = Choices[0];
    }

    public GestureId ImportedId { get; }

    public string Name { get; }

    public string Summary { get; }

    [ObservableProperty]
    public partial Choice<MergeChoice> SelectedChoice { get; set; }

    public MergeChoice Choice
    {
        get => SelectedChoice.Value;
        set => SelectedChoice = Choices.First(choice => choice.Value == value);
    }
}
