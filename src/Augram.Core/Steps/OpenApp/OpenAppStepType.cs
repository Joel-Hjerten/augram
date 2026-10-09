using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.OpenApp;

/// <summary>
/// The "Open app" step type: key <c>openApp</c>, category Run. Parameters: <c>{ "augram": true }</c> for Augram's own window,
/// else <c>{ "windows": "chrome.exe", "mac": "Google Chrome" }</c> (each omitted when empty). Platform-neutral: the step
/// carries both platforms' names, so there is nothing to convert; <see cref="OpenAppStep.AppFor"/> picks this platform's.
/// </summary>
public sealed class OpenAppStepType : IStepType
{
    private OpenAppStepType()
    {
    }

    public static OpenAppStepType Instance { get; } = new();

    internal static HostPlatform CurrentPlatform => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;

    public string Key => "openApp";

    public string DisplayName => "Open app";

    public StepCategory Category => StepCategory.Run;

    public bool IsPlatformNeutral => true;

    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new OpenAppStep(false, string.Empty, string.Empty);

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new OpenAppStep(
            StepParameters.ReadBoolean(parameters, "augram") ?? false,
            StepParameters.ReadString(parameters, "windows") ?? string.Empty,
            StepParameters.ReadString(parameters, "mac") ?? string.Empty);
    }

    public JsonObject Write(IStep step)
    {
        var open = StepParameters.Expect<OpenAppStep>(step, this);
        var written = new JsonObject();
        if (open.IsAugram)
        {
            written["augram"] = true;
        }

        if (open.WindowsApp.Length > 0)
        {
            written["windows"] = open.WindowsApp;
        }

        if (open.MacApp.Length > 0)
        {
            written["mac"] = open.MacApp;
        }

        return written;
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return OpenAppExecutor.Execute(StepParameters.Expect<OpenAppStep>(step, this), context, CurrentPlatform);
    }
}
