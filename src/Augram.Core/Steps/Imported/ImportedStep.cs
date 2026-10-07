namespace Augram.Core.Steps.Imported;

/// <summary>
/// A step the StrokesPlus.net importer brought in that Augram has no type for yet (C1: <c>SendAltDown</c>,
/// <c>ConsumePhysicalInput</c>, script-only actions, …). It keeps the source method, SP.net's own
/// description and the raw <c>MethodParameters</c> as name → value strings, so the command survives the
/// import intact and shows honestly as not supported until a real type replaces it. Running it skips.
/// </summary>
public sealed record ImportedStep(string SourceMethod, string Description, IReadOnlyDictionary<string, string> Parameters) : IStep
{
    public static IReadOnlyDictionary<string, string> NoParameters { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IStepType Type => ImportedStepType.Instance;

    public string Summary => string.IsNullOrEmpty(SourceMethod)
        ? "Imported step (not supported yet)"
        : $"{SourceMethod} (not supported yet)";
}
