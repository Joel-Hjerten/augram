using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.App.Tests.Sync.Support;

/// <summary>
/// One simulated machine for the App's sync tests: real stores, a real <see cref="SyncCoordinator"/>, a temp config
/// folder (state, marker, clone folder), a <see cref="FakeSyncRepository"/> on the shared <see cref="SyncRemote"/>, a
/// store thread that runs on the caller (the sync worker) and a <see cref="SyncService"/> over a
/// <see cref="ManualSchedule"/>, not started until the test says so. A machine used only as "the other one" calls
/// <see cref="SyncDirect"/> and never starts its service.
/// </summary>
internal sealed class SyncMachine : IDisposable
{
    public const string Url = "https://github.com/joel/augram-settings.git";
    public const string OtherUrl = "https://github.com/joel/augram-elsewhere.git";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public SyncMachine(string name, SyncRemote remote, IEnumerable<Gesture>? gestures = null, MappingDocument? mapping = null, string? url = Url, bool autoSync = true)
    {
        Folder = Path.Combine(Path.GetTempPath(), "augram-sync-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        Settings = new SettingsStore(Core.Config.Settings.Default with { Sync = new SyncSettings(url, Guid.NewGuid(), name, autoSync) });
        Gestures = new GestureLibrary(gestures ?? []);
        Mapping = new MappingStore(mapping ?? MappingDocument.Empty);
        Repository = new FakeSyncRepository(remote);
        Folders = new SyncFolders(Folder, Clock);
        Bases = new SyncBaseStore(Folder);
        StoreThread = new SyncStoreThread((apply, _) => apply());
        Coordinator = new SyncCoordinator(Repository, Settings, Gestures, Mapping, Bases, Clock, Log, StoreThread.Run);
        Service = new SyncService(Coordinator, Settings, Gestures, Mapping, Bases, Folders, StoreThread, Clock, Log, Schedule.Schedule);
    }

    public string Folder { get; }

    public SettingsStore Settings { get; }

    public GestureLibrary Gestures { get; }

    public MappingStore Mapping { get; }

    public FakeSyncRepository Repository { get; }

    public SyncFolders Folders { get; }

    public SyncBaseStore Bases { get; }

    public SyncStoreThread StoreThread { get; }

    public SyncCoordinator Coordinator { get; }

    public SyncService Service { get; }

    public ManualSchedule Schedule { get; } = new();

    public ListEventLog Log { get; } = new();

    public IClock Clock { get; } = SystemClock.Instance;

    public static Gesture NewGesture(string name, int dx = 100, int dy = 0)
        => new(GestureId.New(), name, IsActive: true, [new GestureSample([new GesturePoint(0, 0), new GesturePoint(dx, dy)])]);

    /// <summary>A run straight through the coordinator on the test thread, for a machine whose service is not started.</summary>
    public SyncReport SyncDirect(SyncJoin? join = null) => Coordinator.Run(join);

    public Gesture Gesture(string name) => Gestures.All.Single(gesture => gesture.Name == name);

    public bool HasGesture(string name) => Gestures.All.Any(gesture => gesture.Name == name);

    /// <summary>Waits until the service has finished <paramref name="count"/> runs in all.</summary>
    public void WaitForRuns(int count) => WaitFor(() => Service.RunCount >= count, $"{count} sync run(s); {Service.RunCount} so far");

    public static void WaitFor(Func<bool> condition, string what)
    {
        if (!SpinWait.SpinUntil(condition, Timeout))
        {
            throw new TimeoutException($"timed out waiting for {what}");
        }
    }

    public void Dispose()
    {
        Service.Dispose();
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder another test run still holds; the OS cleans temp.
        }
    }
}
