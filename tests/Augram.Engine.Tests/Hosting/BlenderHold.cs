using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.WindowOp;
using Augram.Engine.Tests.Fakes;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Joel's Blender hold remap (plan 0002 step 5) for the engine tests, authored on Windows (the harness's platform): Space with
/// Orbit (Left → Middle), Pan (Right → Shift + Middle), Zoom (Middle → Ctrl + Middle), Zoom both (Left + Right → Ctrl +
/// Middle), Grab (W → G), Scale (E → Ctrl + S), Minimize (Q → a Minimize step, a Steps command) and Zoom wheel (wheel up →
/// Ctrl + wheel up); and a second hold remap on D with Back (Left → Alt + X2), so two hold remaps share Left. No output is a
/// button a test presses physically outside its hold remap (D's Middle would collide with a physical Middle D lets through).
/// </summary>
internal static class BlenderHold
{
    public const string Process = "blender.exe";

    public static readonly WindowIdentity Window = FakeWindowSystem.Identity(Process, handle: 0x70, root: 0x7000);

    public static AppGroup Group()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var d = HoldRemap.For(KeyCode.D);
        var commands = new[]
        {
            Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), new RemapOutput.Button(MouseButton.Middle)),
            Remap(space, "Pan", HoldInput.Of(MouseButton.Right), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift)),
            Remap(space, "Zoom", HoldInput.Of(MouseButton.Middle), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control)),
            Remap(space, "Zoom both", HoldInput.Of(MouseButton.Left, MouseButton.Right), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control)),
            Remap(space, "Grab", new HoldInput.Key(KeyCode.W), new RemapOutput.Key(KeyCode.G)),
            Remap(space, "Scale", new HoldInput.Key(KeyCode.E), new RemapOutput.Key(KeyCode.S, KeyModifiers.Control)),
            Remap(space, "Zoom wheel", new HoldInput.Wheel(WheelDirection.Up), new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Control)),
            new Command(CommandId.New(), "Minimize", Trigger.ForInput(new HoldInput.Key(KeyCode.Q)), IsActive: true, [new CommandStep(new WindowOpStep(WindowOperation.Minimize), HostPlatform.Windows)]) { HoldRemapId = space.Id },
            Remap(d, "Back", HoldInput.Of(MouseButton.Left), new RemapOutput.Button(MouseButton.X2, KeyModifiers.Alt)),
        };
        return new AppGroup(GroupId.New(), "Blender", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = [Process] }, commands) { HoldRemaps = [space, d] };
    }

    /// <summary>Global and Blender, validated as the store would; <paramref name="ignored"/> puts Blender on the ignore list too (over the app: its stroke button passes through).</summary>
    public static MappingDocument Document(bool ignored = false)
    {
        IgnoredApp[] ignore = ignored ? [new IgnoredApp(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = [Process] }, DisableEntirely: false)] : [];
        return MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal, Group()], ignore));
    }

    /// <summary>The plan the hook reads with Blender in front, on Windows.</summary>
    public static HoldRemapPlan Plan() => HoldRemapPlan.For(Document(), Window, HostPlatform.Windows);

    private static Command Remap(HoldRemap holdRemap, string name, HoldInput input, RemapOutput output)
        => new(CommandId.New(), name, Trigger.ForInput(input), IsActive: true, [new CommandStep(new RemapStep(output), HostPlatform.Windows)]) { HoldRemapId = holdRemap.Id };
}
