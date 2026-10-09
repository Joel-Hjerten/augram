namespace Augram.Core.Steps.Unknown;

/// <summary>
/// A step this Augram cannot read, kept exactly as the file had it (Joel, 2026-10-09): a type a newer Augram added
/// (<see cref="Reason"/> "unknown step type 'scroll'"), or parameters its type refuses (a value a newer Augram added). The
/// type key and the <c>params</c> object are written back unchanged, so an older build that saves no longer deletes what
/// it does not understand; the step stays inside its command and goes when the command goes. Running it skips.
/// <see cref="ParametersJson"/> is the <c>params</c> object as compact JSON text, so equal steps are equal records.
/// </summary>
public sealed record UnknownStep(string StoredTypeKey, string ParametersJson, string Reason) : IStep
{
    public IStepType Type => UnknownStepType.Instance;

    /// <summary>The key the file had, not <see cref="UnknownStepType"/>'s: what the config writer puts back.</summary>
    public string StoredKey => StoredTypeKey;

    public string Summary => $"'{StoredTypeKey}' step (needs a newer Augram)";
}
