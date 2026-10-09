using Augram.Core.Abstractions;

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

    /// <summary>
    /// <see cref="Summary"/> in <paramref name="platform"/>'s words, for a step shown away from where it was authored: a
    /// Windows hotkey with the Windows key reads "Win+D" on a Mac too. Only platform-bound types differ from <see cref="Summary"/>.
    /// </summary>
    string SummaryOn(HostPlatform platform) => Summary;

    /// <summary>
    /// <see cref="Summary"/> fit for the log files: the same line unless the step can hold something private. Type text
    /// gives its length, never its text; Run gives the program, never its arguments (a typed ID number or a password on a
    /// command line must not reach a log).
    /// </summary>
    string LogSummary => Summary;

    /// <summary>
    /// The type key the config file holds for this step: <see cref="IStepType.Key"/>, except for a step kept as is from a
    /// newer Augram (<c>Steps/Unknown</c>), which writes back the key it was read with.
    /// </summary>
    string StoredKey => Type.Key;
}
