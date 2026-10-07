namespace Augram.Core.Steps;

/// <summary>
/// One executable unit of a command (F5a): an immutable record owned by its <see cref="Type"/>'s folder.
/// A step knows only its own parameters; how it is edited, stored and run is the type's business.
/// </summary>
public interface IStep
{
    IStepType Type { get; }

    /// <summary>One short line for a command row's step summary: "Minimize window", "Ctrl+W", "Wait 30 ms".</summary>
    string Summary { get; }
}
