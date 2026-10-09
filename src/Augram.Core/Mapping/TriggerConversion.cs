using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// A command's trigger as it reads on a platform (F8 with Joel's 2026-10-09 decision: trigger keys convert like hotkeys do):
/// the trigger itself, a best-guess conversion, or none with the reason ("Win + gesture needs a macOS version"). Computed at
/// resolve and display time, never stored. <see cref="Convert"/> follows <c>Steps/Hotkey/HotkeyConversion</c>'s modifier
/// rules: Windows Ctrl is Cmd on a Mac and Mac Cmd is Ctrl on Windows, Alt is Option on both, Shift stays; the Windows key has
/// no Mac counterpart and a Mac set holding both Cmd and Ctrl has no Windows one. Buttons and the kind never change. (The
/// hotkey exception table is about whole key combinations, which a trigger has none of.)
/// </summary>
public sealed record TriggerConversion(Trigger? Trigger, string? Reason)
{
    /// <summary>True when the trigger was changed for the platform (its keys swapped).</summary>
    public bool IsConverted { get; init; }

    public static TriggerConversion Same(Trigger trigger) => new(trigger, null);

    public static TriggerConversion To(Trigger trigger) => new(trigger, null) { IsConverted = true };

    public static TriggerConversion None(string reason) => new(null, reason);

    /// <summary><paramref name="trigger"/> authored on <paramref name="from"/>, as it runs on <paramref name="to"/>.</summary>
    public static TriggerConversion Convert(Trigger trigger, HostPlatform from, HostPlatform to)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var keys = trigger.Hold.Keys;
        if (from == to || keys == KeyModifiers.None || !trigger.IsBound)
        {
            return Same(trigger);
        }

        if (from == HostPlatform.Windows)
        {
            return (keys & KeyModifiers.Meta) != 0
                ? None($"{trigger.Describe(HostPlatform.Windows)} needs a macOS version: the Windows key has no Mac counterpart")
                : Swapped(trigger, KeyModifiers.Control, KeyModifiers.Meta);
        }

        return (keys & (KeyModifiers.Meta | KeyModifiers.Control)) == (KeyModifiers.Meta | KeyModifiers.Control)
            ? None($"{trigger.Describe(HostPlatform.MacOS)} needs a Windows version: Cmd and Ctrl together have no Windows counterpart")
            : Swapped(trigger, KeyModifiers.Meta, KeyModifiers.Control);
    }

    private static TriggerConversion Swapped(Trigger trigger, KeyModifiers from, KeyModifiers to)
    {
        var keys = trigger.Hold.Keys;
        if ((keys & from) == 0)
        {
            return Same(trigger);
        }

        return To(trigger with { Hold = trigger.Hold with { Keys = (keys & ~from) | to } });
    }

    /// <summary>For display: "Ctrl + gesture → Cmd + gesture", or the reason there is none.</summary>
    public string Explain(Trigger original, HostPlatform from, HostPlatform to)
    {
        ArgumentNullException.ThrowIfNull(original);
        return Trigger is null ? Reason ?? string.Empty : $"{original.Describe(from)} → {Trigger.Describe(to)}";
    }
}
