using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Config;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Mapping.Support;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>
/// One simulated machine: its own stores, its own temp state folder, a <see cref="FakeSyncRepository"/> on the
/// shared <see cref="SyncRemote"/>, and a coordinator whose store thread is the test thread unless
/// <see cref="StoreThread"/> says otherwise. Steps are <see cref="FakeStepType"/> steps.
/// </summary>
internal sealed class SyncMachine : IDisposable
{
    public const string Url = "https://github.com/joel/augram-settings.git";

    private readonly TempFolder _folder = new();

    public SyncMachine(string name, SyncRemote remote, SteppingClock clock, IEnumerable<Gesture>? gestures = null, MappingDocument? mapping = null, Guid? machineId = null)
    {
        Settings = new SettingsStore(Core.Config.Settings.Default with { Sync = new SyncSettings(Url, machineId ?? Guid.NewGuid(), name) });
        Gestures = new GestureLibrary(gestures ?? []);
        Mapping = new MappingStore(mapping ?? MappingDocument.Empty);
        Repository = new FakeSyncRepository(remote);
        Bases = new SyncBaseStore(_folder.Path);
        Coordinator = new SyncCoordinator(Repository, Settings, Gestures, Mapping, Bases, clock, Log, f => StoreThread(f), FakeStepType.Registry);
    }

    public SettingsStore Settings { get; }

    public GestureLibrary Gestures { get; }

    public MappingStore Mapping { get; }

    public FakeSyncRepository Repository { get; }

    public SyncBaseStore Bases { get; }

    public CountingEventLog Log { get; } = new();

    public SyncCoordinator Coordinator { get; }

    /// <summary>How the coordinator reaches the stores; the test thread by default.</summary>
    public Func<Func<SyncApplied>, SyncApplied> StoreThread { get; set; } = f => f();

    public Guid Id => Settings.Current.Sync.MachineId;

    public SyncItemSet Items => SyncItemSet.From(Gestures.All, Mapping.Current);

    public SyncReport Sync(SyncJoin? join = null) => Coordinator.Run(join);

    public SyncReport Resolve(SyncConflict conflict, SyncChoice choice) => Coordinator.Resolve(conflict, choice);

    public Command Command(string name) => Mapping.Current.AllCommands().Single(pair => pair.Command.Name == name).Command;

    public Gesture Gesture(string name) => Gestures.All.Single(gesture => gesture.Name == name);

    /// <summary>True when both machines hold exactly the same items.</summary>
    public bool SameAs(SyncMachine other) => Items.SameAs(other.Items.Contents());

    public void Dispose() => _folder.Dispose();
}
