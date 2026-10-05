namespace Augram.Import.StrokesPlus;

/// <summary>How much attention an <see cref="ImportWarning"/> deserves.</summary>
public enum ImportSeverity
{
    /// <summary>Nothing was lost; worth knowing (for example a stock 2-point gesture).</summary>
    Info,

    /// <summary>Something was skipped, dropped or renamed.</summary>
    Warning,
}
