using System.Collections.Frozen;
using Avalonia.Input;
using CoreKey = Augram.Core.Abstractions.KeyCode;
using CoreModifiers = Augram.Core.Abstractions.KeyModifiers;
using UiModifiers = Avalonia.Input.KeyModifiers;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// The no-engine fallback's translation of window key events: Avalonia's layout-independent
/// <see cref="PhysicalKey"/> to Core's <see cref="CoreKey"/> (both name US-layout key positions, so most
/// names match; the rest are listed), and Avalonia's modifier flags, whose bits differ from Core's.
/// </summary>
public static class AvaloniaKeyMap
{
    private static readonly FrozenDictionary<PhysicalKey, CoreKey> Renamed = new Dictionary<PhysicalKey, CoreKey>
    {
        [PhysicalKey.Backquote] = CoreKey.BackQuote,
        [PhysicalKey.BracketLeft] = CoreKey.OpenBracket,
        [PhysicalKey.BracketRight] = CoreKey.CloseBracket,
        [PhysicalKey.Equal] = CoreKey.Equals,
        [PhysicalKey.AltLeft] = CoreKey.LeftAlt,
        [PhysicalKey.AltRight] = CoreKey.RightAlt,
        [PhysicalKey.ControlLeft] = CoreKey.LeftControl,
        [PhysicalKey.ControlRight] = CoreKey.RightControl,
        [PhysicalKey.MetaLeft] = CoreKey.LeftMeta,
        [PhysicalKey.MetaRight] = CoreKey.RightMeta,
        [PhysicalKey.ShiftLeft] = CoreKey.LeftShift,
        [PhysicalKey.ShiftRight] = CoreKey.RightShift,
        [PhysicalKey.ArrowDown] = CoreKey.Down,
        [PhysicalKey.ArrowLeft] = CoreKey.Left,
        [PhysicalKey.ArrowRight] = CoreKey.Right,
        [PhysicalKey.ArrowUp] = CoreKey.Up,
        [PhysicalKey.LaunchApp2] = CoreKey.AppCalculator,
        [PhysicalKey.LaunchMail] = CoreKey.AppMail,
        [PhysicalKey.MediaPlayPause] = CoreKey.MediaPlay,
        [PhysicalKey.MediaTrackNext] = CoreKey.MediaNext,
        [PhysicalKey.MediaTrackPrevious] = CoreKey.MediaPrevious,
        [PhysicalKey.AudioVolumeDown] = CoreKey.VolumeDown,
        [PhysicalKey.AudioVolumeMute] = CoreKey.VolumeMute,
        [PhysicalKey.AudioVolumeUp] = CoreKey.VolumeUp,
    }.ToFrozenDictionary();

    /// <summary><see cref="CoreKey.None"/> for a key Core has no name for (IME keys, the international extras).</summary>
    public static CoreKey ToKeyCode(PhysicalKey key)
    {
        if (key == PhysicalKey.None)
        {
            return CoreKey.None;
        }

        if (Renamed.TryGetValue(key, out var renamed))
        {
            return renamed;
        }

        return Enum.TryParse<CoreKey>(key.ToString(), ignoreCase: false, out var core) ? core : CoreKey.None;
    }

    public static CoreModifiers ToModifiers(UiModifiers modifiers)
    {
        var result = CoreModifiers.None;
        if (modifiers.HasFlag(UiModifiers.Control))
        {
            result |= CoreModifiers.Control;
        }

        if (modifiers.HasFlag(UiModifiers.Alt))
        {
            result |= CoreModifiers.Alt;
        }

        if (modifiers.HasFlag(UiModifiers.Shift))
        {
            result |= CoreModifiers.Shift;
        }

        if (modifiers.HasFlag(UiModifiers.Meta))
        {
            result |= CoreModifiers.Meta;
        }

        return result;
    }
}
