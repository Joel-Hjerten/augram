namespace Augram.Core.Steps.MediaKey;

/// <summary>Taps one media or volume key (F5). The same key means the same thing on every platform, so there is nothing to convert (F8).</summary>
public sealed record MediaKeyStep(MediaKeyKind Key) : IStep
{
    public IStepType Type => MediaKeyStepType.Instance;

    public string Summary => Key switch
    {
        MediaKeyKind.VolumeUp => "Volume up",
        MediaKeyKind.VolumeDown => "Volume down",
        MediaKeyKind.VolumeMute => "Mute",
        MediaKeyKind.PlayPause => "Play/pause",
        MediaKeyKind.NextTrack => "Next track",
        MediaKeyKind.PreviousTrack => "Previous track",
        MediaKeyKind.Stop => "Stop",
        _ => Key.ToString(),
    };
}
