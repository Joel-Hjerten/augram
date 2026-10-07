using Augram.Core.Abstractions;

namespace Augram.Core.Steps.MediaKey;

/// <summary>The one place a <see cref="MediaKeyKind"/> becomes a <see cref="KeyCode"/>; a test proves every kind maps.</summary>
public static class MediaKeyKindExtensions
{
    public static KeyCode ToKeyCode(this MediaKeyKind kind) => kind switch
    {
        MediaKeyKind.VolumeUp => KeyCode.VolumeUp,
        MediaKeyKind.VolumeDown => KeyCode.VolumeDown,
        MediaKeyKind.VolumeMute => KeyCode.VolumeMute,
        MediaKeyKind.PlayPause => KeyCode.MediaPlay,
        MediaKeyKind.NextTrack => KeyCode.MediaNext,
        MediaKeyKind.PreviousTrack => KeyCode.MediaPrevious,
        MediaKeyKind.Stop => KeyCode.MediaStop,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a media key kind."),
    };
}
