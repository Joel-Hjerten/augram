using Augram.Core.Steps;
using Augram.Core.Steps.ClearClipboard;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// What a StrokesPlus.net script-only action becomes (plan 0001 §C1; reference config §4, "Script actions reduce to four
/// things"). Shared by a fresh import (<see cref="ActionReader"/>) and the upgrade of saved placeholders
/// (<see cref="PlaceholderUpgrade"/>), so both turn the same script into the same steps. In order:
/// <list type="bullet">
/// <item>one plain <c>sp.RunProgram</c> call (<see cref="RunProgramScript"/>) → the step <see cref="ProgramCallMapping"/> makes;</item>
/// <item>only comments and blanks → no steps: the command does nothing here, an override to nothing (SP.net's "Ignore …"
/// commands that silence a Global gesture over the desktop);</item>
/// <item>one <c>sp.SendKeys("…")</c> call with a plain string → the steps <see cref="SendKeysSyntax"/> makes, when every
/// part maps (<c>^{ADD}</c> is Ctrl + Num +);</item>
/// <item><c>clip.Clear()</c> alone → <see cref="ClearClipboardStep"/>;</item>
/// <item>one of <see cref="SampleScripts"/> (half-screen snap, Ctrl + wheel post) → its step.</item>
/// </list>
/// Comments, blanks and semicolons never matter. Anything else is null: the caller keeps the placeholder. Pure.
/// </summary>
public static class ScriptMapping
{
    private static readonly string[] SendKeysCall = ["sp", ".", "SendKeys", "("];

    private static readonly string[] ClearClipboardCall = ["clip", ".", "Clear", "(", ")"];

    /// <summary>The steps that do what <paramref name="script"/> did (possibly none); null when Augram has no equivalent.</summary>
    public static IReadOnlyList<IStep>? TryMap(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (RunProgramScript.TryRecognize(script, out var call, out _))
        {
            return [ProgramCallMapping.ToStep(call)];
        }

        if (ScriptTokens.Read(script) is not { } tokens)
        {
            return null;
        }

        if (tokens.Count == 0)
        {
            return [];
        }

        if (SendKeysText(tokens) is { } keys)
        {
            return SendKeysSyntax.Parse(keys) is { IsComplete: true } parsed ? parsed.Steps : null;
        }

        if (ScriptTokens.Same(tokens, ClearClipboardCall))
        {
            return [new ClearClipboardStep()];
        }

        return SampleScripts.Match(tokens) is { } sample ? [sample] : null;
    }

    /// <summary>The key string of <c>sp.SendKeys("…")</c> when that call is the whole script; null otherwise.</summary>
    private static string? SendKeysText(IReadOnlyList<string> tokens)
    {
        if (tokens.Count != SendKeysCall.Length + 2 || !tokens.Take(SendKeysCall.Length).SequenceEqual(SendKeysCall, StringComparer.Ordinal) || tokens[^1] != ")")
        {
            return null;
        }

        var literal = tokens[SendKeysCall.Length];
        return literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"' ? literal[1..^1] : null;
    }
}
