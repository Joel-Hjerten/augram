namespace Augram.Import.StrokesPlus;

/// <summary>Resolution of a <see cref="MergeKind.Conflict"/> entry (requirements F8).</summary>
public enum MergeChoice
{
    /// <summary>Leave the existing gesture untouched; the imported one is discarded.</summary>
    KeepMine,

    /// <summary>Replace the existing gesture's name, samples and active flag; its id is kept so references stay valid.</summary>
    TakeTheirs,

    /// <summary>Keep the existing gesture and add the imported one, renamed "Name (2)" when the name is taken.</summary>
    KeepBoth,
}
