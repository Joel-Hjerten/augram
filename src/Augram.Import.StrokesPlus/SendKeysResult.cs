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

    /// <summary>
    /// True when the steps can stand in for the string: every part mapped and they are at least one step (<c>{LEFT 0}</c>
    /// parses cleanly into none). A fresh import, the upgrade of a saved placeholder and a <c>sp.SendKeys</c> script all keep
    /// the placeholder otherwise, so the same string always ends the same way.
    /// </summary>
    public bool IsComplete => IsClean && Steps.Count > 0;
}
