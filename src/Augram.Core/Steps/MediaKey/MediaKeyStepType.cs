using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.MediaKey;

/// <summary>
/// The "Media key" step type: key <c>mediaKey</c>, category System, platform-neutral. Parameters:
/// <c>{ "key": "VolumeUp" }</c>, one of <see cref="MediaKeyKind"/>'s names; the default instance is Volume up.
/// </summary>
public sealed class MediaKeyStepType : IStepType
{
    private MediaKeyStepType()
    {
    }

    public static MediaKeyStepType Instance { get; } = new();

    public string Key => "mediaKey";

    public string DisplayName => "Media key";

    public StepCategory Category => StepCategory.System;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new MediaKeyStep(MediaKeyKind.VolumeUp);

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var key = StepParameters.ReadEnum<MediaKeyKind>(parameters, "key") ?? MediaKeyKind.VolumeUp;
        return new MediaKeyStep(key);
    }

    public JsonObject Write(IStep step)
    {
        var mediaKey = StepParameters.Expect<MediaKeyStep>(step, this);
        return new JsonObject { ["key"] = mediaKey.Key.ToString() };
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return MediaKeyExecutor.Execute(StepParameters.Expect<MediaKeyStep>(step, this), context);
    }
}
