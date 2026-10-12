#if DEBUG
using Augram.App.Components.Capture;
using Augram.App.Components.CommandHeader;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;
using Avalonia.Data;
using Avalonia.Threading;
using MouseButton = Augram.Core.Capture.MouseButton;

namespace Augram.App.DevGallery;

/// <summary>
/// The capture fields (<c>Components/Capture/</c>) in their states, each over a fake port, so nothing here ever arms the real
/// engine: the hotkey field idle, capturing with the engine (what is held shows live), capturing without it (only this window's
/// keys), in one-key mode refusing a modifier, and after the engine ended the capture; the button detector of a hold remap
/// command's Add button… listening with the engine and without it. The live states start when the page opens and end as they
/// do in a form: the first click anywhere accepts the hotkey fields, and the detector takes that click as its press (or gives up
/// after 5 s and says so).
/// </summary>
internal static class CaptureGalleryPage
{
    public static ScreenDeclaration Page() => new FormScreen("Capture fields",
    [
        new Section("HotkeyCaptureBox, idle: a combination with a right-hand key, and none",
        [
            new CustomField("Set", () => Box(new NoKeyCapture(), modifiers: KeyModifiers.Control | KeyModifiers.Alt, rightHand: KeyModifiers.Alt, key: KeyCode.F9)),
            new CustomField("Empty", () => Box(new NoKeyCapture())),
        ]),
        new Section("Capturing with the engine: every key comes here; Ctrl and Shift held so far",
        [
            new CustomField("Engine", () => Capturing(Box(new ScriptedKeyCapture(KeyCaptureEvent.KeyDown(KeyCode.LeftControl, KeyModifiers.Control), KeyCaptureEvent.KeyDown(KeyCode.LeftShift, KeyModifiers.Control | KeyModifiers.Shift))))),
        ]),
        new Section("Capturing without the engine (--no-engine): only this window's keys",
        [
            new CustomField("Window only", () => Capturing(Box(new NoKeyCapture()))),
        ]),
        new Section("One key (a hold key, an input): a modifier is refused, and the field says why",
        [
            new CustomField("One key", () => Capturing(Box(new ScriptedKeyCapture(KeyCaptureEvent.KeyDown(KeyCode.LeftControl, KeyModifiers.Control)), singleKey: true))),
        ]),
        new Section("The engine ended the capture (10 s without a key): the field keeps its key and says why",
        [
            new CustomField("Timed out", () => Capturing(Box(new ScriptedKeyCapture(KeyCaptureEvent.Released(KeyCaptureEvent.IdleTimeoutReason)), key: KeyCode.F9))),
        ]),
        new Section("ButtonDetector (Blender › Pan's Add button…), listening with the engine: a press anywhere",
        [
            new CustomField("Engine", () => Detecting(new ListeningButtonCapture())),
        ]),
        new Section("ButtonDetector listening without the engine: a press over this window",
        [
            new CustomField("Window only", () => Detecting(new NoButtonCapture())),
        ]),
    ]);

    private static HotkeyCaptureBox Box(IKeyCapture capture, KeyModifiers modifiers = KeyModifiers.None, KeyModifiers rightHand = KeyModifiers.None, KeyCode key = KeyCode.None, bool singleKey = false)
        => new() { KeyCapture = capture, Modifiers = modifiers, RightHand = rightHand, Key = key, SingleKey = singleKey };

    /// <summary>Starts the field capturing once it is in a window, as a click on it would.</summary>
    private static HotkeyCaptureBox Capturing(HotkeyCaptureBox box)
    {
        box.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(box.BeginCapture);
        return box;
    }

    /// <summary>Blender › Pan's header over a throwaway store, its Add button… listening through <paramref name="capture"/> once it is in a window.</summary>
    private static CommandHeader Detecting(IButtonCapture capture)
    {
        var library = new GestureLibrary(StarterGestures.All());
        var store = new MappingStore(CommandGalleryFakes.Mapping());
        var vm = new CommandsViewModel(CommandsScope.Apps, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), HostPlatform.Windows);
        vm.ShowCommand(store.Current.AllCommands().Single(pair => pair.Command.Name == "Pan").Command.Id);
        var header = new CommandHeader { Width = 760, ButtonCapture = capture };
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        header.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(header.DetectButton);
        return header;
    }

    /// <summary>No engine: the field falls back to its window's keys.</summary>
    private sealed class NoKeyCapture : IKeyCapture
    {
        public IDisposable? Begin(Action<KeyCaptureEvent> onEvent, TimeSpan idleTimeout) => null;
    }

    /// <summary>An engine capture that reports <paramref name="events"/> once the field is capturing, then nothing more.</summary>
    private sealed class ScriptedKeyCapture(params KeyCaptureEvent[] events) : IKeyCapture
    {
        public IDisposable? Begin(Action<KeyCaptureEvent> onEvent, TimeSpan idleTimeout)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var captured in events)
                {
                    onEvent(captured);
                }
            });
            return new Handle();
        }
    }

    /// <summary>An engine detection that waits for a press that never comes.</summary>
    private sealed class ListeningButtonCapture : IButtonCapture
    {
        public IDisposable? Begin(Action<MouseButton> onPress) => new Handle();
    }

    /// <summary>No engine: the detector takes a press on its window only.</summary>
    private sealed class NoButtonCapture : IButtonCapture
    {
        public IDisposable? Begin(Action<MouseButton> onPress) => null;
    }

    private sealed class Handle : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
#endif
