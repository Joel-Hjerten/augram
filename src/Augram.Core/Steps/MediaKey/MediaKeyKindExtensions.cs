using Augram.Core.Abstractions;

namespace Augram.Core.Steps.MediaKey;

/// <summary>The one place a <see cref="MediaKeyKind"/> becomes a <see cref="KeyCode"/>, and back; a test proves every kind maps.</summary>
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

    /// <summary>The kind whose <see cref="ToKeyCode"/> is <paramref name="key"/>; null for a key that is not a media key.</summary>
    public static MediaKeyKind? FromKeyCode(KeyCode key)
    {
        foreach (var kind in Enum.GetValues<MediaKeyKind>())
        {
            if (kind.ToKeyCode() == key)
            {
                return kind;
            }
        }

        return null;
    }
}
