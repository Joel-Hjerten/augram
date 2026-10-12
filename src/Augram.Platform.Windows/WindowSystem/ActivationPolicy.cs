using Augram.Core.Abstractions;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// Rule A20, as SP.net learned it the hard way: activate the window under the gesture start only when it is not already
/// in front and is not the desktop. Activating anyway broke popup and find windows (Notepad++, XTranslate) and Edge's
/// window hierarchy, because the popup's owner stole focus back from the popup.
/// <para>
/// "In front" compares <b>top-level windows</b> (<c>GA_ROOT</c>), never root owners (plan 0008, 2026-10-12): after a
/// minimize, Windows hands the foreground to the next top-level window in the stacking order, which for PotPlayer was a
/// title-less helper window owned by the main window. Its root owner is the main window, so a root-owner comparison
/// called the main window "in front" while the keys went to the helper. A gesture over a focused find dialog still
/// compares equal (the dialog is its own top-level window), so the popups A20 protects stay untouched.
/// </para>
/// A disabled target is one of its own modal dialogs holding the input: the keys belong to that dialog, and
/// <c>SetForegroundWindow</c> on the disabled owner would only bounce back to it, so it is left alone too.
/// </summary>
internal static class ActivationPolicy
{
    public static bool NeedsActivation(WindowIdentity target, nint targetTopLevel, nint foregroundTopLevel, bool targetEnabled = true)
    {
        if (target.IsDesktop || targetTopLevel == 0 || !targetEnabled)
        {
            return false;
        }

        return targetTopLevel != foregroundTopLevel;
    }
}
