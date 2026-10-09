using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.Mapping.Support;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.HoldRemaps.Support;

/// <summary>
/// Joel's Blender setup (plan 0002 step 5), the hold remap the tests are written against: Space with Orbit (Left → Middle),
/// Pan (Right → Shift + Middle), Zoom (Middle → Ctrl + Middle), Zoom both (Left + Right → Ctrl + Middle), Grab (W → G),
/// Scale (E → S), Rotate (R → Ctrl + Shift + Alt + R), Frame (F → Num .), plus two Steps commands for the tests: Note (Q)
/// and Nudge (wheel down). Fresh ids per call.
/// </summary>
internal static class Blender
{
    public static readonly RemapOutput Middle = new RemapOutput.Button(MouseButton.Middle);
    public static readonly RemapOutput ShiftMiddle = new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift);
    public static readonly RemapOutput CtrlMiddle = new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control);
    public static readonly RemapOutput G = new RemapOutput.Key(KeyCode.G);

    public static HoldRemap NewSpace() => HoldRemap.For(KeyCode.Space);

    /// <summary>A Remap command under <paramref name="holdRemap"/>.</summary>
    public static Command Remap(HoldRemap holdRemap, string name, HoldInput input, RemapOutput output, HostPlatform authoredOn = HostPlatform.Windows)
        => new Command(CommandId.New(), name, Trigger.ForInput(input), IsActive: true, [new CommandStep(new RemapStep(output), authoredOn)]) { HoldRemapId = holdRemap.Id };

    /// <summary>A Steps command under <paramref name="holdRemap"/> (one fake step).</summary>
    public static Command Steps(HoldRemap holdRemap, string name, HoldInput input)
        => new Command(CommandId.New(), name, Trigger.ForInput(input), IsActive: true, [NewStep(name)]) { HoldRemapId = holdRemap.Id };

    /// <summary>The Blender app group with its Space hold remap and the commands above.</summary>
    public static AppGroup Group(HoldRemap? space = null)
    {
        var hold = space ?? NewSpace();
        var group = NewGroup("Blender", ByProcess("blender.exe"),
            Remap(hold, "Orbit", HoldInput.Of(MouseButton.Left), Middle),
            Remap(hold, "Pan", HoldInput.Of(MouseButton.Right), ShiftMiddle),
            Remap(hold, "Zoom", HoldInput.Of(MouseButton.Middle), CtrlMiddle),
            Remap(hold, "Zoom both", HoldInput.Of(MouseButton.Left, MouseButton.Right), CtrlMiddle),
            Remap(hold, "Grab", new HoldInput.Key(KeyCode.W), G),
            Remap(hold, "Scale", new HoldInput.Key(KeyCode.E), new RemapOutput.Key(KeyCode.S)),
            Remap(hold, "Rotate", new HoldInput.Key(KeyCode.R), new RemapOutput.Key(KeyCode.R, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt)),
            Remap(hold, "Frame", new HoldInput.Key(KeyCode.F), new RemapOutput.Key(KeyCode.NumPadDecimal)),
            Steps(hold, "Note", new HoldInput.Key(KeyCode.Q)),
            Steps(hold, "Nudge", new HoldInput.Wheel(WheelDirection.Down)));
        return group with { HoldRemaps = [hold] };
    }

    /// <summary>A valid document with Global and the Blender group.</summary>
    public static MappingDocument Document(AppGroup? blender = null)
        => MappingRules.ValidDocument(MappingFixtures.Document(NewGlobal(), blender ?? Group()));

    /// <summary>The Space hold remap as the hook and the machine see it on Windows.</summary>
    public static HoldRemapEntry Entry(HostPlatform platform = HostPlatform.Windows)
        => HoldRemapPlan.ForGroup(Document().Groups[1], platform).Entries.Single();
}
