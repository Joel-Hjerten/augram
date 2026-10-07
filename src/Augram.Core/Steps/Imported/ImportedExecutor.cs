using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.Imported;

/// <summary>Never runs anything: Skipped with the source method in the reason, and one Info line so the Diagnostics tab shows which imported steps a command still carries.</summary>
internal static class ImportedExecutor
{
    public static StepResult Execute(ImportedStep step, StepExecutionContext context)
    {
        context.Log.Info("steps", "Imported step skipped", ("method", step.SourceMethod), ("description", step.Description));
        return StepResult.Skipped($"imported step '{step.SourceMethod}' is not supported yet");
    }
}
