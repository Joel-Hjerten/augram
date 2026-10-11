using Augram.Core.Sync;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// The names taken by one kind of imported item in one scope (the gestures, the app groups, the ignored apps, one group's
/// commands). SP.net allows a repeated name and Augram does not, so a repeat is renamed by Core's rule
/// (<see cref="SyncNames"/>: the first free " (2)", " (3)"…, compared case-insensitively) and reported once as
/// "Duplicate <i>kind</i> name; imported as '…'.". Every reader that names items claims through this.
/// </summary>
internal sealed class ImportedNames
{
    private readonly SyncNames _names;
    private readonly string _kind;
    private readonly List<ImportWarning> _warnings;

    /// <param name="kind">The item as the warning names it: "gesture", "app", "ignored app", "command".</param>
    /// <param name="warnings">The import report.</param>
    /// <param name="taken">Names no item may take as written ("Global" for the app groups).</param>
    public ImportedNames(string kind, List<ImportWarning> warnings, params IEnumerable<string> taken)
    {
        _names = new SyncNames(taken);
        _kind = kind;
        _warnings = warnings;
    }

    /// <summary><paramref name="name"/> when it is free, else its first free numbered form, with a warning. Either way it is taken afterwards.</summary>
    public string Claim(string name)
    {
        var claimed = _names.Claim(name);
        if (claimed != name)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, name, $"Duplicate {_kind} name; imported as '{claimed}'."));
        }

        return claimed;
    }
}
