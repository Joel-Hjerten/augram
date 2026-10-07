namespace Augram.Core.Steps.MediaKey;

/// <summary>
/// The media and volume keys a <see cref="MediaKeyStep"/> can press (F5; Joel's 14 SP.net
/// <c>SendVKey</c> actions). Each maps to one platform-neutral <see cref="Abstractions.KeyCode"/>
/// in <see cref="MediaKeyKindExtensions"/>; names are stable because they end up in the config file.
/// </summary>
public enum MediaKeyKind
{
    VolumeUp,
    VolumeDown,
    VolumeMute,

    /// <summary>The single play/pause toggle key every keyboard and OS exposes (there is no separate pause).</summary>
    PlayPause,
    NextTrack,
    PreviousTrack,
    Stop,
}
