using System.Globalization;
using Augram.App.Declarations;
using Augram.Core.Gestures;
using Augram.Import.StrokesPlus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Import;

/// <summary>
/// One entry of the merge plan that needs a decision (F8): a name clash or, per A7, an imported
/// gesture whose shape scores as a duplicate of an existing one. Carries the user's
/// <see cref="MergeChoice"/>; default KeepMine, which for a shape match means the imported commands
/// bind to the gesture already in the library.
/// </summary>
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
        Kind = entry.Kind;
        var theirs = $"theirs: {entry.Imported.Samples.Count} sample(s), {(entry.Imported.IsActive ? "active" : "inactive")}";
        var mine = $"mine: {entry.Existing.Samples.Count} sample(s), {(entry.Existing.IsActive ? "active" : "inactive")}";
        Summary = entry.Kind == MergeKind.SameShape
            ? string.Create(CultureInfo.InvariantCulture, $"same shape as '{entry.Existing.Name}' (score {entry.ShapeScore ?? 0:0}) · {theirs} · {mine}")
            : $"{theirs} · {mine}";
        SelectedChoice = Choices[0];
    }

    public GestureId ImportedId { get; }

    public string Name { get; }

    public MergeKind Kind { get; }

    public string Summary { get; }

    [ObservableProperty]
    public partial Choice<MergeChoice> SelectedChoice { get; set; }

    public MergeChoice Choice
    {
        get => SelectedChoice.Value;
        set => SelectedChoice = Choices.First(choice => choice.Value == value);
    }
}
