using System.Diagnostics.CodeAnalysis;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Recognises a StrokesPlus.net script that does exactly one thing, run a program (plan 0001 §C1: <c>sp.RunProgram(file,
/// args, verb, style, …)</c> in scripts → a Run step): its only statement, comments and blanks aside and one trailing
/// semicolon allowed, is a single <c>sp.RunProgram(fileName, arguments, verb, style, useShellExecute, noWindow,
/// waitForExit)</c> call whose four strings are literals (double or single quotes, backticks, <c>String.raw</c>
/// templates, joined with <c>+</c>) and whose three flags are <c>true</c> or <c>false</c>. In the file name only,
/// <c>sp.ExpandEnvironmentVariables("…")</c> is read as its text, since the Run step expands <c>%VAR%</c> there itself.
/// Anything else (variables, other calls, a second statement) is refused with a reason for the import report. Pure; the
/// caller decides what the call becomes (<see cref="RunMapping.FromRunProgram"/>, or the Display step for a display changer).
/// </summary>
public static class RunProgramScript
{
    public const int ParameterCount = 7;

    private static readonly string[] ParameterNames = ["fileName", "arguments", "verb", "style", "useShellExecute", "noWindow", "waitForExit"];

    /// <summary>The call when <paramref name="script"/> is one plain <c>sp.RunProgram</c> call; otherwise false with the reason.</summary>
    public static bool TryRecognize(string script, [NotNullWhen(true)] out RunProgramCall? call, out string reason)
    {
        ArgumentNullException.ThrowIfNull(script);
        call = null;
        var reader = new ScriptReader(script);
        if (!reader.SkipTrivia())
        {
            reason = "a comment is not closed";
            return false;
        }

        if (reader.AtEnd)
        {
            reason = "the script is empty";
            return false;
        }

        if (!(reader.TryWord("sp") && reader.TryChar('.') && reader.TryWord("RunProgram") && reader.TryChar('(')))
        {
            reason = "the script is not a single sp.RunProgram call";
            return false;
        }

        var texts = new string[4];
        var flags = new bool[3];
        for (var index = 0; index < ParameterCount; index++)
        {
            if (index > 0 && !reader.TryChar(','))
            {
                reason = reader.TryChar(')')
                    ? $"sp.RunProgram has {index} argument(s); it takes {ParameterCount}"
                    : "the sp.RunProgram arguments are not plain values";
                return false;
            }

            if (index < texts.Length)
            {
                var text = ReadString(reader, allowExpand: index == 0);
                if (text is null)
                {
                    reason = $"{ParameterNames[index]} is not a plain string" + (reader.Error is { } error ? $" ({error})" : string.Empty);
                    return false;
                }

                texts[index] = text;
            }
            else if (reader.TryWord("true"))
            {
                flags[index - texts.Length] = true;
            }
            else if (!reader.TryWord("false"))
            {
                reason = $"{ParameterNames[index]} is not true or false";
                return false;
            }
        }

        if (!reader.TryChar(')'))
        {
            reason = reader.TryChar(',')
                ? $"sp.RunProgram takes {ParameterCount} arguments; this call has more"
                : "the sp.RunProgram arguments are not plain values";
            return false;
        }

        reader.TryChar(';');
        if (!reader.AtEnd)
        {
            reason = "the script does more than one sp.RunProgram call";
            return false;
        }

        call = new RunProgramCall(texts[0], texts[1], texts[2], texts[3], flags[0], flags[1], flags[2]);
        reason = string.Empty;
        return true;
    }

    /// <summary>Terms joined by <c>+</c>; null when any term is not a plain string.</summary>
    private static string? ReadString(ScriptReader reader, bool allowExpand)
    {
        var value = ReadTerm(reader, allowExpand);
        while (value is not null && reader.TryChar('+'))
        {
            var next = ReadTerm(reader, allowExpand);
            value = next is null ? null : value + next;
        }

        return value;
    }

    /// <summary>A string literal, a <c>String.raw</c> template, or (file name only) <c>sp.ExpandEnvironmentVariables(…)</c> read as its argument's text.</summary>
    private static string? ReadTerm(ScriptReader reader, bool allowExpand)
    {
        if (reader.TryString() is { } literal)
        {
            return literal;
        }

        if (reader.Error is not null)
        {
            return null;
        }

        if (reader.TryWord("String"))
        {
            return reader.TryChar('.') && reader.TryWord("raw") ? reader.TryRawTemplate() : null;
        }

        if (allowExpand && reader.TryWord("sp") && reader.TryChar('.') && reader.TryWord("ExpandEnvironmentVariables") && reader.TryChar('('))
        {
            var inner = ReadString(reader, allowExpand: false);
            return inner is not null && reader.TryChar(')') ? inner : null;
        }

        return null;
    }
}
