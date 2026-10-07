namespace Augram.Core.Mapping;

public enum ResolutionOutcome
{
    /// <summary>A command was found (possibly an override to nothing: check <see cref="Command.IsOverrideToNothing"/>).</summary>
    Matched,

    /// <summary>No command for this trigger here; the reason says why.</summary>
    None,

    /// <summary>The window belongs to an ignored app; nothing fires there.</summary>
    Ignored,
}
