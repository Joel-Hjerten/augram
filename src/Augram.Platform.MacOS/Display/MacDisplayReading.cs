using Augram.Platform.MacOS.WindowSystem;

namespace Augram.Platform.MacOS.Display;

/// <summary>One active display as <see cref="MacDisplayList"/> reads it: its <c>CGDirectDisplayID</c>, bounds in global points, flags, current mode and every mode.</summary>
internal sealed record MacDisplayReading(
    uint Id,
    MacRect Bounds,
    bool IsMain,
    bool IsBuiltIn,
    MacNativeMode? Current,
    IReadOnlyList<MacNativeMode> Modes);
