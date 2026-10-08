using Augram.Core.Steps.Imported;
using Augram.Core.Steps.TypeText;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns SP.net's text methods into steps (plan 0001 §C1), pure and stateless like <see cref="HotkeyMapping"/> so the
/// step reader and a later upgrade of saved placeholders share it. <c>SendKeys</c> carries <c>sendKeysString</c> in key
/// syntax, parsed by <see cref="SendKeysSyntax"/> into several steps (text runs, hotkeys, delays); <c>SendString</c> carries
/// <c>characters</c>, plain text typed as it is: one <see cref="TypeTextStep"/>, no syntax. Both answer null when the
/// parameter is missing or empty, so the caller keeps its placeholder. A SendKeys result can still hold no steps (a string
/// of nothing but modifiers); its warnings say why, and keeping a placeholder then is the caller's choice.
/// </summary>
public static class TextMapping
{
    /// <summary><c>SendKeys</c>'s parameters as steps; null when <c>sendKeysString</c> is missing or empty.</summary>
    public static SendKeysResult? FromSendKeys(IReadOnlyDictionary<string, string> parameters, TypeTextMethod textMethod = TypeTextMethod.Unicode)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return parameters.TryGetValue(StrokesPlusJson.Method.SendKeysParameter, out var keys) && keys.Length > 0
            ? SendKeysSyntax.Parse(keys, textMethod)
            : null;
    }

    /// <summary><c>SendString</c>'s parameters as one Type text step with the characters exactly as written; null when <c>characters</c> is missing or empty.</summary>
    public static TypeTextStep? FromSendString(IReadOnlyDictionary<string, string> parameters, TypeTextMethod method = TypeTextMethod.Unicode)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return parameters.TryGetValue(StrokesPlusJson.Method.SendStringParameter, out var characters) && characters.Length > 0
            ? new TypeTextStep(characters, method)
            : null;
    }

    /// <summary>
    /// A placeholder an earlier import stored for <c>SendKeys</c> or <c>SendString</c>, as the steps that replace it; null for
    /// any other method or when the parameter is missing. Lets a saved config be upgraded without re-importing.
    /// </summary>
    public static SendKeysResult? TryUpgrade(ImportedStep step, TypeTextMethod textMethod = TypeTextMethod.Unicode)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.SourceMethod switch
        {
            StrokesPlusJson.Method.SendKeys => FromSendKeys(step.Parameters, textMethod),
            StrokesPlusJson.Method.SendString => FromSendString(step.Parameters, textMethod) is { } typed ? new SendKeysResult([typed], []) : null,
            _ => null,
        };
    }
}
