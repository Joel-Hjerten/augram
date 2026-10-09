using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.OpenApp;

/// <summary>
/// Runs an <see cref="OpenAppStep"/> on the executor thread: Augram's own window through <see cref="StepExecutionContext.AppWindow"/>;
/// otherwise this platform's app is brought to the front through <see cref="StepExecutionContext.Apps"/>, and started through
/// <see cref="StepExecutionContext.Processes"/> when it has no window open. No activation or settle delay of the gesture's
/// window: the step's point is a different app. One Debug line per run, naming the app, never more.
/// </summary>
internal static class OpenAppExecutor
{
    public static StepResult Execute(OpenAppStep step, StepExecutionContext context, HostPlatform platform)
    {
        var (result, how) = Run(step, context, platform);
        context.Log.Debug("steps", "Open app", ("app", step.IsAugram ? "Augram" : step.AppFor(platform)), ("how", how), ("outcome", result.Outcome), ("reason", result.Reason));
        return result;
    }

    private static (StepResult Result, string How) Run(OpenAppStep step, StepExecutionContext context, HostPlatform platform)
    {
        if (step.IsAugram)
        {
            return context.AppWindow.Open() ? (StepResult.Done, "own window") : (StepResult.Skipped("Augram has no window to open here"), "own window");
        }

        if (step.AppFor(platform) is not { } app)
        {
            return (StepResult.Skipped(step.IsSet ? $"no app set for {(platform == HostPlatform.MacOS ? "macOS" : "Windows")} and no guess" : "no app set"), "none");
        }

        var activation = context.Apps.BringToFront(app);
        switch (activation.Outcome)
        {
            case AppActivationOutcome.Activated:
                return (StepResult.Done, "brought to front");
            case AppActivationOutcome.Failed:
                return (StepResult.Failed(activation.Reason ?? $"{app} could not be brought to the front"), "brought to front");
        }

        if (context.Cancellation.IsCancellationRequested)
        {
            return (StepResult.Skipped("cancelled"), "launch");
        }

        var launched = context.Processes.Launch(new ProcessLaunch(app));
        var result = launched.Outcome switch
        {
            ProcessLaunchOutcome.Started => StepResult.Done,
            ProcessLaunchOutcome.Cancelled or ProcessLaunchOutcome.NotSupported => StepResult.Skipped(launched.Reason ?? $"{app} could not be started here"),
            _ => StepResult.Failed(launched.Reason ?? $"{app} could not be started"),
        };
        return (result, "launch");
    }
}
