using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.TypeText;

/// <summary>
/// Types a <see cref="TypeTextStep"/> line by line: each line through <see cref="IInputSimulator.TypeText"/> or
/// <see cref="IInputSimulator.TypeTextByKeys"/> by the method, and a real Enter key press between lines (a line break
/// sent as a character is not an Enter key to most apps). No text → Skipped (the command goes on). Typing by keys checks
/// the whole text first, so a character a US keyboard lacks fails the step before anything is typed. A simulator answer
/// other than success → Failed with it (the command stops). Cancellation is honoured before every line (Skipped
/// "cancelled"). The settle delay after an activation (A8) is the executor's, applied before this step, never here. One
/// Debug line per run with the text's length and line count, never the text: it may be private.
/// </summary>
internal static class TypeTextExecutor
{
    public const string NoTextReason = "no text set";

    public const string CancelledReason = "cancelled";

    public static StepResult Execute(TypeTextStep step, StepExecutionContext context)
    {
        var lines = step.Lines();
        var result = step.HasText ? Run(step.Method, lines, context) : StepResult.Skipped(NoTextReason);
        context.Log.Debug(
            "steps",
            "Type text",
            ("method", step.Method),
            ("length", step.Text?.Length ?? 0),
            ("lines", lines.Count),
            ("outcome", result.Outcome),
            ("reason", result.Reason));
        return result;
    }

    /// <summary>Why a by-keys text cannot be typed: the 1-based position of the first character without a key, never the character.</summary>
    public static string NoKeyReason(int position) =>
        $"typing by keys: character {position} has no key on a US layout; Unicode types any character";

    private static StepResult Run(TypeTextMethod method, IReadOnlyList<string> lines, StepExecutionContext context)
    {
        if (method == TypeTextMethod.Keys && FirstUntypeable(lines) is { } position)
        {
            return StepResult.Failed(NoKeyReason(position));
        }

        var input = context.Input;
        for (var index = 0; index < lines.Count; index++)
        {
            if (context.Cancellation.IsCancellationRequested)
            {
                return StepResult.Skipped(CancelledReason);
            }

            if (index > 0)
            {
                var enter = input.Hotkey(KeyModifiers.None, KeyCode.Enter);
                if (enter != SimulationResult.Success)
                {
                    return StepResult.Failed($"Enter between lines: {enter}");
                }
            }

            var line = lines[index];
            if (line.Length == 0)
            {
                continue;
            }

            var typed = method == TypeTextMethod.Keys ? input.TypeTextByKeys(line) : input.TypeText(line);
            if (typed != SimulationResult.Success)
            {
                return StepResult.Failed(method == TypeTextMethod.Keys ? $"typing by keys: {typed}" : $"Unicode typing: {typed}");
            }
        }

        return StepResult.Done;
    }

    /// <summary>The 1-based position in the whole text (a line break counted as one) of the first character without a key; null when all have one.</summary>
    private static int? FirstUntypeable(IReadOnlyList<string> lines)
    {
        var offset = 0;
        foreach (var line in lines)
        {
            var index = AsciiKeyLayout.IndexOfUntypeable(line);
            if (index >= 0)
            {
                return offset + index + 1;
            }

            offset += line.Length + 1;
        }

        return null;
    }
}
