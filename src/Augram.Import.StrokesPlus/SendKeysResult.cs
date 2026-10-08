using Augram.Core.Steps;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// What <see cref="SendKeysSyntax.Parse"/> made of one SendKeys string: the steps in order (Type text, Hotkey, Media key,
/// Delay) and one sentence per part it could not map or had to change, for the import report. The sentences name the
/// part, not the action; the caller wraps each in an <see cref="ImportWarning"/> with the action's name.
/// </summary>
public sealed record SendKeysResult(IReadOnlyList<IStep> Steps, IReadOnlyList<string> Warnings)
{
    /// <summary>True when every part mapped as written.</summary>
    public bool IsClean => Warnings.Count == 0;
}
