using Augram.Core.Abstractions;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// Rule A20, as SP.net learned it the hard way: activate the root owner under the gesture start only when it differs
/// from the foreground root and is not the desktop. Activating anyway broke popup and find windows (Notepad++, XTranslate)
/// and Edge's window hierarchy, because the popup's owner stole focus back from the popup.
/// </summary>
internal static class ActivationPolicy
{
    public static bool NeedsActivation(WindowIdentity target, nint foregroundRoot)
    {
        if (target.IsDesktop || target.RootHandle == 0)
        {
            return false;
        }

        return target.RootHandle != foregroundRoot;
    }
}
