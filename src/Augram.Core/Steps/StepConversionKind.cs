namespace Augram.Core.Steps;

/// <summary>What a step authored on one platform does on the other (F8): runs as is, runs converted, or has no sensible guess.</summary>
public enum StepConversionKind
{
    Unchanged,
    Converted,
    NotConvertible,
}
