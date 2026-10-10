#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.UsedBy;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.WindowOp;

namespace Augram.App.DevGallery;

/// <summary>
/// The fake mapping and the instant-answer presenters behind the Commands gallery pages: Global with the
/// categories Window and Media, "PC tools" (Use on Windows only: its command's header shows the macOS box disabled
/// with "Set by category") and "Mac tools" (macOS only: listed greyed with Show other platforms), and one
/// uncategorized command; Chrome (with trigger combinations: Right + wheel up, Shift + gesture, Ctrl + Left + click),
/// Steam games (an override to nothing), Photoshop with categories and Blender with its Space hold remap (plan 0002), so every
/// section kind, tag, marker, trigger badge and input has a case.
/// </summary>
public static class CommandGalleryFakes
{
    public static MappingDocument Mapping()
    {
        var window = new CommandCategory(CategoryId.New(), "Window");
        var media = new CommandCategory(CategoryId.New(), "Media");
        var pcTools = new CommandCategory(CategoryId.New(), "PC tools") { UseOn = PlatformSet.Windows };
        var macTools = new CommandCategory(CategoryId.New(), "Mac tools") { UseOn = PlatformSet.MacOS };
        var global = AppGroup.EmptyGlobal with
        {
            Categories = [window, media, pcTools, macTools],
            Commands =
            [
                Cmd("Close window", Trigger.ForGesture(StarterGestures.IdFor("Up")), new WindowOpStep(WindowOperation.Close)) with { CategoryId = window.Id },
                Cmd("Minimize", Trigger.ForGesture(StarterGestures.IdFor("Down")), new WindowOpStep(WindowOperation.Minimize)) with { IsActive = false, CategoryId = window.Id },
                Cmd("Volume up", Trigger.ForWheel(WheelDirection.Up), new MediaKeyStep(MediaKeyKind.VolumeUp)) with { CategoryId = media.Id },
                Cmd("Volume down", Trigger.ForWheel(WheelDirection.Down), new MediaKeyStep(MediaKeyKind.VolumeDown)) with { CategoryId = media.Id },
                Cmd("Unbound", Trigger.None, new DelayStep(30)),
                Cmd("Show desktop", Trigger.ForGesture(StarterGestures.IdFor("Left")), new DelayStep(10)) with { CategoryId = pcTools.Id },
                Cmd("Mission Control", Trigger.ForGesture(StarterGestures.IdFor("Right")), new DelayStep(10)) with { CategoryId = macTools.Id },
            ],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false,
            new AppMatcher { WindowsProcessNames = ["chrome.exe", "msedge.exe"] },
            [
                Cmd("Close tab", Trigger.ForGesture(StarterGestures.IdFor("Up")), new ImportedStep("SendKeys", "Send Ctrl+W", new Dictionary<string, string> { ["Keys"] = "^w" })),
                ZoomReset(),

                // F1 combinations (Joel, 2026-10-09): an anchor other than the stroke button, a gesture with a key, a click trigger.
                Cmd("Zoom in", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), new DelayStep(10)),
                Cmd("Reopen tab", Trigger.ForGesture(StarterGestures.IdFor("Up"), TriggerHold.WithStroke(KeyModifiers.Shift, capture: HoldCapture.Before)), new DelayStep(10)),
                Cmd("Open in background", Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.Control, HeldButtons.Left)), new DelayStep(10)),
            ]);
        var steam = new AppGroup(GroupId.New(), "Steam games", IsActive: true, SuppressGlobals: true,
            new AppMatcher { Title = "^.*\\(Steam\\)$", TitleIsRegex = true },
            [Cmd("Nothing on Up", Trigger.ForGesture(StarterGestures.IdFor("Up")))]);
        var general = new CommandCategory(CategoryId.New(), "General");
        var blend = new CommandCategory(CategoryId.New(), "Blend Mode Normal");
        var photoshop = new AppGroup(GroupId.New(), "Photoshop", IsActive: true, SuppressGlobals: false,
            new AppMatcher { WindowsProcessNames = ["photoshop.exe"] },
            [
                Cmd("Undo", Trigger.ForGesture(StarterGestures.IdFor("Left")), new DelayStep(10)) with { CategoryId = general.Id },
                Cmd("Multiply", Trigger.ForGesture(StarterGestures.IdFor("Right")), new DelayStep(10)) with { CategoryId = blend.Id },
                Cmd("Brush size", Trigger.ForWheel(WheelDirection.Up), new DelayStep(10)),
            ],
            [general, blend]);
        return new MappingDocument([global, chrome, steam, photoshop, Blender()], []);
    }

    /// <summary>
    /// Joel's Blender (F9, plan 0002): the Space hold remap with Orbit (Left → Middle), Pan (Right → Shift + Middle), Zoom both
    /// (Left + Right → Ctrl + Middle) and Grab (W → G), beside one ordinary command; the hold remap is nested in the group.
    /// </summary>
    public static AppGroup Blender()
    {
        var space = HoldRemap.For(KeyCode.Space);
        Command Remap(string name, HoldInput input, RemapOutput output)
            => new Command(CommandId.New(), name, Trigger.ForInput(input), IsActive: true, [new CommandStep(new RemapStep(output), HostPlatform.Windows)]) { HoldRemapId = space.Id };
        return new AppGroup(GroupId.New(), "Blender", IsActive: true, SuppressGlobals: false,
            new AppMatcher { WindowsProcessNames = ["blender.exe"], MacProcessNames = ["Blender"] },
            [
                Cmd("Undo", Trigger.ForGesture(StarterGestures.IdFor("Left")), new DelayStep(10)),
                Remap("Orbit", HoldInput.Of(MouseButton.Left), new RemapOutput.Button(MouseButton.Middle)),
                Remap("Pan", HoldInput.Of(MouseButton.Right), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift)),
                Remap("Zoom both", HoldInput.Of(MouseButton.Left, MouseButton.Right), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control)),
                Remap("Grab", new HoldInput.Key(KeyCode.W), new RemapOutput.Key(KeyCode.G)),
            ])
        {
            HoldRemaps = [space],
        };
    }

    /// <summary>A Windows command with its own macOS steps (F8), so the gallery shows the own-version marker.</summary>
    private static Command ZoomReset()
    {
        var original = new Command(CommandId.New(), "Zoom reset", Trigger.ForGesture(StarterGestures.IdFor("Circle")), IsActive: true,
        [
            new CommandStep(new DelayStep(50), HostPlatform.Windows),
            new CommandStep(new WindowOpStep(WindowOperation.Center), HostPlatform.Windows),
        ]);
        return original.WithStepsFor(HostPlatform.MacOS, [new CommandStep(new DelayStep(120), HostPlatform.MacOS)], DateTimeOffset.UnixEpoch);
    }

    private static Command Cmd(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, [.. steps.Select(step => new CommandStep(step, HostPlatform.Windows))]);

    /// <summary>No window in the gallery: answers with the last starter gesture at once.</summary>
    public sealed class GesturePicker : IGesturePickerPresenter
    {
        private readonly GestureLibrary _library;

        public GesturePicker(GestureLibrary library)
        {
            _library = library;
        }

        public Task<GesturePickerResult> PickAsync(GestureId? current)
            => Task.FromResult(GesturePickerResult.Selected(_library.All[^1].Id));
    }

    /// <summary>No window in the gallery: every form is confirmed at once (a new group without a name shows the rule message).</summary>
    public sealed class FormDialogs : IFormDialogPresenter
    {
        public Task<bool> ShowAsync(FormDialogRequest request) => Task.FromResult(true);
    }

    /// <summary>No window in the gallery: every delete is confirmed at once; Undo brings it back.</summary>
    public sealed class Confirm : IConfirmPresenter
    {
        public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) => Task.FromResult(true);
    }
}
#endif
