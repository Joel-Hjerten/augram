#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.UsedBy;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;

namespace Augram.App.DevGallery;

/// <summary>
/// The fake mapping and the instant-answer presenters behind the Commands gallery pages: Global with two
/// categories (Window, Media) and one uncategorized command, Chrome, Steam games (an override to
/// nothing) and Photoshop with categories, so every section kind, tag and marker has a case.
/// </summary>
public static class CommandGalleryFakes
{
    public static MappingDocument Mapping()
    {
        var window = new CommandCategory(CategoryId.New(), "Window");
        var media = new CommandCategory(CategoryId.New(), "Media");
        var global = AppGroup.EmptyGlobal with
        {
            Categories = [window, media],
            Commands =
            [
                Cmd("Close window", Trigger.ForGesture(StarterGestures.IdFor("Up")), new WindowOpStep(WindowOperation.Close)) with { CategoryId = window.Id },
                Cmd("Minimize", Trigger.ForGesture(StarterGestures.IdFor("Down")), new WindowOpStep(WindowOperation.Minimize)) with { IsActive = false, CategoryId = window.Id },
                Cmd("Volume up", Trigger.ForWheel(WheelDirection.Up), new MediaKeyStep(MediaKeyKind.VolumeUp)) with { CategoryId = media.Id },
                Cmd("Volume down", Trigger.ForWheel(WheelDirection.Down), new MediaKeyStep(MediaKeyKind.VolumeDown)) with { CategoryId = media.Id },
                Cmd("Unbound", Trigger.None, new DelayStep(30)),
            ],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false,
            new AppMatcher { WindowsProcessNames = ["chrome.exe", "msedge.exe"] },
            [
                Cmd("Close tab", Trigger.ForGesture(StarterGestures.IdFor("Up")), new ImportedStep("SendKeys", "Send Ctrl+W", new Dictionary<string, string> { ["Keys"] = "^w" })),
                new(CommandId.New(), "Zoom reset", Trigger.ForGesture(StarterGestures.IdFor("Circle")), IsActive: true,
                [
                    new CommandStep(new DelayStep(50), HostPlatform.Windows, MacOsOverride: new DelayStep(120)),
                    new CommandStep(new WindowOpStep(WindowOperation.Center), HostPlatform.Windows),
                ]),
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
        return new MappingDocument([global, chrome, steam, photoshop], []);
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
