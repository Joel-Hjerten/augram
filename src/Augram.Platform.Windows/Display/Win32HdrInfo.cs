using Augram.Core.Abstractions;

namespace Augram.Platform.Windows.Display;

/// <summary>
/// A display's HDR read from the CCD advanced colour bits (learnings 0002 §2). Windows 11 24H2 and later answer
/// <c>GET_ADVANCED_COLOR_INFO_2</c>: bit 4 <c>highDynamicRangeSupported</c>, bit 5 <c>highDynamicRangeUserEnabled</c>,
/// and HDR is switched with <c>SET_HDR_STATE</c>. Older Windows only answer <c>GET_ADVANCED_COLOR_INFO</c>: bit 0
/// <c>advancedColorSupported</c>, bit 1 <c>advancedColorEnabled</c>, switched with <c>SET_ADVANCED_COLOR_STATE</c>. The
/// newer answer wins because on 24H2 "advanced colour" also covers wide colour on SDR displays, which is not HDR.
/// </summary>
internal readonly record struct Win32HdrInfo(HdrState State, bool UsesHdrState)
{
    public static Win32HdrInfo From(Win32DisplayTarget? target)
    {
        if (target?.AdvancedColorInfo2 is { } info2)
        {
            return new(ToState(supported: (info2 & 0x10) != 0, on: (info2 & 0x20) != 0), UsesHdrState: true);
        }

        if (target?.AdvancedColorInfo is { } info)
        {
            return new(ToState(supported: (info & 0x1) != 0, on: (info & 0x2) != 0), UsesHdrState: false);
        }

        return new(HdrState.Unsupported, UsesHdrState: false);
    }

    private static HdrState ToState(bool supported, bool on) => !supported ? HdrState.Unsupported : on ? HdrState.On : HdrState.Off;
}
