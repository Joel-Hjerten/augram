using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Run;

/// <summary>
/// The "Run" step type: key <c>run</c>, category Run, platform-bound (F8: a program path belongs to one platform).
/// Parameters: <c>{ "file": "taskkill.exe", "arguments": "/f /im yuzu.exe", "workingDirectory": "", "elevated": true, "hidden": true }</c>,
/// all five always written so the round trip is byte-stable; a missing member takes its default (empty text, false).
/// The default instance is <see cref="RunStep.Unset"/>, which runs as Skipped "no program set".
/// </summary>
public sealed class RunStepType : IStepType
{
    public const string FileMember = "file";

    public const string ArgumentsMember = "arguments";

    public const string WorkingDirectoryMember = "workingDirectory";

    public const string ElevatedMember = "elevated";

    public const string HiddenMember = "hidden";

    private RunStepType()
    {
    }

    public static RunStepType Instance { get; } = new();

    public string Key => "run";

    public string DisplayName => "Run";

    public StepCategory Category => StepCategory.Run;

    public bool IsPlatformNeutral => false;

    /// <summary>The arguments may hold a secret (a token, a password): an export says they travel as written (plan 0003).</summary>
    public bool MayHoldPrivateText => true;

    /// <summary>Cross-platform links (http, https, mailto, ftp) unchanged; anything else needs its own version (<see cref="RunConversion"/>).</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to)
        => RunConversion.Convert(StepParameters.Expect<RunStep>(step, this), from, to);

    public IStep CreateDefault() => RunStep.Unset;

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new RunStep(
            StepParameters.ReadString(parameters, FileMember) ?? string.Empty,
            StepParameters.ReadString(parameters, ArgumentsMember) ?? string.Empty,
            StepParameters.ReadString(parameters, WorkingDirectoryMember) ?? string.Empty,
            StepParameters.ReadBoolean(parameters, ElevatedMember) ?? false,
            StepParameters.ReadBoolean(parameters, HiddenMember) ?? false);
    }

    public JsonObject Write(IStep step)
    {
        var run = StepParameters.Expect<RunStep>(step, this);
        return new JsonObject
        {
            [FileMember] = run.File,
            [ArgumentsMember] = run.Arguments,
            [WorkingDirectoryMember] = run.WorkingDirectory,
            [ElevatedMember] = run.Elevated,
            [HiddenMember] = run.Hidden,
        };
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RunExecutor.Execute(StepParameters.Expect<RunStep>(step, this), context);
    }
}
