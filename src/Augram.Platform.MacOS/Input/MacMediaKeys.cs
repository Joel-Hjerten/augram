using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The pure half of <see cref="MacMediaKeySimulator"/>, tested on every OS. macOS takes the volume and playback keys only as
/// system-defined events (<c>NSEventTypeSystemDefined</c>, subtype 8 "aux control button"), never as key codes: the key's
/// <c>NX_KEYTYPE_*</c> number sits in the high 16 bits of <c>data1</c>, the state (0xA down, 0xB up) in the next byte, and the
/// same state shifted once more is the event's modifier flags. There is no Stop key on a Mac.
/// </summary>
internal static class MacMediaKeys
{
    public const int SystemDefinedType = 14;
    public const short AuxControlButtons = 8;

    // NX_KEYTYPE_* from IOKit/hidsystem/ev_keymap.h.
    private const int SoundUp = 0;
    private const int SoundDown = 1;
    private const int Mute = 7;
    private const int Play = 16;
    private const int Next = 17;
    private const int Previous = 18;

    private const int Down = 0xA;
    private const int Up = 0xB;

    /// <summary>The <c>NX_KEYTYPE_*</c> for <paramref name="key"/>; false for every key that is not a Mac media key (Stop included).</summary>
    public static bool TryMap(KeyCode key, out int keyType)
    {
        keyType = key switch
        {
            KeyCode.VolumeUp => SoundUp,
            KeyCode.VolumeDown => SoundDown,
            KeyCode.VolumeMute => Mute,
            KeyCode.MediaPlay => Play,
            KeyCode.MediaNext => Next,
            KeyCode.MediaPrevious => Previous,
            _ => -1,
        };
        return keyType >= 0;
    }

    public static nint Data1(int keyType, bool pressed) => (keyType << 16) | ((pressed ? Down : Up) << 8);

    public static nuint ModifierFlags(bool pressed) => (nuint)((pressed ? Down : Up) << 8);
}
