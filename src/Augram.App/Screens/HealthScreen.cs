using Augram.App.Declarations;
using Augram.App.ViewModels;

namespace Augram.App.Screens;

/// <summary>Diagnostics › Health (N4): read-only notes fed from the health registry.</summary>
public static class HealthScreen
{
    public static ScreenDeclaration Declare(HealthViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new FormScreen("Health",
        [
            new Section("Hook",
            [
                new NoteField("Hook alive", new DelegateBinding<string>(() => vm.HookAlive, owner: vm)),
                new NoteField("Reinstalls", new DelegateBinding<string>(() => vm.HookReinstalls, owner: vm)),
                new NoteField("Events in the last minute", new DelegateBinding<string>(() => vm.EventsLastMinute, owner: vm)),
            ]),
            new Section("Last stroke",
            [
                new NoteField("Stroke latency", new DelegateBinding<string>(() => vm.LastStrokeLatency, owner: vm)),
                new NoteField("Activation", new DelegateBinding<string>(() => vm.LastActivation, owner: vm)),
                new NoteField("Overlay first frame", new DelegateBinding<string>(() => vm.OverlayFirstFrame, owner: vm)),
            ]),
            new Section("Process",
            [
                new NoteField("Uptime", new DelegateBinding<string>(() => vm.Uptime, owner: vm)),
                new NoteField("Memory (working set)", new DelegateBinding<string>(() => vm.Memory, owner: vm)),
                new NoteField("Last sync", new DelegateBinding<string>(() => vm.LastSync, owner: vm), "Details on Options › Sync."),
            ], "Refreshes every second while the window is open."),
        ]);
    }
}
