using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS;

/// <summary>
/// The Accessibility permission (System Settings › Privacy &amp; Security › Accessibility). The hook's event tap, injected
/// input and every window operation need it. macOS grants it to the responsible app: Augram when it runs as an app,
/// the terminal or editor that started it during development.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacAccessibility
{
    public static bool IsTrusted() => Ax.IsTrusted;
}
