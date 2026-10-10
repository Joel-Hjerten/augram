namespace Augram.Core.Transfer;

/// <summary>What an item of the file is against this configuration: the sync's merge table with no base (plan 0003).</summary>
public enum ImportStatus
{
    /// <summary>Not here: the import adds it.</summary>
    New,

    /// <summary>Here with the same content: nothing to do.</summary>
    Same,

    /// <summary>Here with other content: a conflict, Keep mine until the user chooses.</summary>
    Different,
}
