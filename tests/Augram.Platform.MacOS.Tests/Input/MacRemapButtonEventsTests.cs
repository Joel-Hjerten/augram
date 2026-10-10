using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Platform.MacOS.Input;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Input;

/// <summary>
/// A hold remap's button output as CoreGraphics takes it (plan 0002 step 3a, learnings 0005): the event types and button
/// number per button, and the flagsChanged events and mouse-down flags its modifiers become. The single-modifier cases are
/// exactly what the spike posted when Blender panned and zoomed.
/// </summary>
public sealed class MacRemapButtonEventsTests
{
    [Theory]
    [InlineData(MouseButton.Left, 1u, 2u, 6u, 0u)]
    [InlineData(MouseButton.Right, 3u, 4u, 7u, 1u)]
    [InlineData(MouseButton.Middle, 25u, 26u, 27u, 2u)]
    [InlineData(MouseButton.X1, 25u, 26u, 27u, 3u)]
    [InlineData(MouseButton.X2, 25u, 26u, 27u, 4u)]
    public void EachButtonHasItsDownUpAndDraggedTypes_AndItsButtonNumber(MouseButton button, uint down, uint up, uint dragged, uint number)
        => Assert.Equal(new MacMouseEvents(down, up, dragged, number), MacRemapButtonEvents.For(button));

    [Fact]
    public void Shift_AsTheSpikePostedIt()
    {
        var events = MacRemapButtonEvents.Modifiers(KeyModifiers.Shift);

        // kVK_Shift, kCGEventFlagMaskShift | NX_DEVICELSHIFTKEYMASK | NX_NONCOALSESCEDMASK; the up leaves only 0x100.
        Assert.Equal([new MacFlagsChange(56, 0x2_0102)], events.Downs);
        Assert.Equal(0x2_0002UL, events.MouseDownFlags);
        Assert.Equal([new MacFlagsChange(56, 0x100)], events.Ups);
    }

    [Fact]
    public void Control_AsTheSpikePostedIt()
    {
        var events = MacRemapButtonEvents.Modifiers(KeyModifiers.Control);

        Assert.Equal([new MacFlagsChange(59, 0x4_0101)], events.Downs);
        Assert.Equal(0x4_0001UL, events.MouseDownFlags);
        Assert.Equal([new MacFlagsChange(59, 0x100)], events.Ups);
    }

    [Fact]
    public void AllFour_GoDownInCtrlOptionShiftCommandOrder_EachCarryingWhatIsHeld_AndUpInReverse()
    {
        var events = MacRemapButtonEvents.Modifiers(KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control);

        Assert.Equal(
            [
                new MacFlagsChange(59, 0x04_0101),
                new MacFlagsChange(58, 0x0C_0121),
                new MacFlagsChange(56, 0x0E_0123),
                new MacFlagsChange(55, 0x1E_012B),
            ],
            events.Downs);
        Assert.Equal(0x1E_002BUL, events.MouseDownFlags);
        Assert.Equal(
            [
                new MacFlagsChange(55, 0x0E_0123),
                new MacFlagsChange(56, 0x0C_0121),
                new MacFlagsChange(58, 0x04_0101),
                new MacFlagsChange(59, 0x100),
            ],
            events.Ups);
    }

    [Fact]
    public void NoModifiers_PostNoFlagsChangedAndSetNoFlags()
    {
        var events = MacRemapButtonEvents.Modifiers(KeyModifiers.None);

        Assert.Empty(events.Downs);
        Assert.Empty(events.Ups);
        Assert.Equal(0UL, events.MouseDownFlags);
    }
}
