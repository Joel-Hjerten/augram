using Augram.Core.Config;
using Augram.Core.Gestures;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class FileConfigStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly List<string> _notices = [];
    private DateTime _now = new(2026, 10, 5, 12, 0, 0);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void SaveWritesTheMainFileAndLeavesNoTempFile()
    {
        var store = NewStore();

        store.Save(SampleDocuments.WithGesture("A"));

        Assert.True(File.Exists(store.Location));
        Assert.Equal([FileConfigStore.FileName], Directory.GetFiles(_folder.Path).Select(path => Path.GetFileName(path)));
        Assert.Equal("A", Assert.Single(ConfigSerializer.Read(File.ReadAllText(store.Location)).Gestures).Name);
    }

    [Fact]
    public void SaveBacksUpThePreviousFileBeforeReplacingIt()
    {
        var store = NewStore();
        store.Save(SampleDocuments.WithGesture("A"));
        _now = _now.AddSeconds(1);

        store.Save(SampleDocuments.WithGesture("B"));

        var backup = Assert.Single(store.Backups.NewestFirst());
        Assert.Equal("augram-20261005-120001.json", Path.GetFileName(backup));
        Assert.Equal("A", Assert.Single(ConfigSerializer.Read(File.ReadAllText(backup)).Gestures).Name);
        Assert.Equal("B", Assert.Single(store.Load().Gestures).Name);
    }

    [Fact]
    public void SavesWithinOneSecondGetDistinctBackupNamesInAgeOrder()
    {
        var store = NewStore();
        for (int i = 0; i < 4; i++)
        {
            store.Save(SampleDocuments.WithGesture($"G{i}"));
        }

        var names = store.Backups.NewestFirst().Select(path => Path.GetFileName(path)).ToArray();

        Assert.Equal(["augram-20261005-120000-3.json", "augram-20261005-120000-2.json", "augram-20261005-120000.json"], names);
        Assert.Equal("G2", Assert.Single(ConfigSerializer.Read(File.ReadAllText(store.Backups.NewestFirst()[0])).Gestures).Name);
    }

    [Fact]
    public void BackupsArePrunedToTheNewestTwenty()
    {
        var store = NewStore();

        for (int i = 0; i < 25; i++)
        {
            store.Save(SampleDocuments.WithGesture($"G{i}"));
            _now = _now.AddSeconds(1);
        }

        var backups = store.Backups.NewestFirst();
        Assert.Equal(ConfigBackups.DefaultKeep, backups.Count);
        Assert.Equal("augram-20261005-120024.json", Path.GetFileName(backups[0]));
        Assert.Equal("augram-20261005-120005.json", Path.GetFileName(backups[^1]));
        Assert.Equal("G23", Assert.Single(ConfigSerializer.Read(File.ReadAllText(backups[0])).Gestures).Name);
    }

    [Fact]
    public void LoadReadsTheMainFileWithoutNotices()
    {
        var store = NewStore();
        store.Save(SampleDocuments.Full());

        var loaded = store.Load();

        Assert.Equal(SampleDocuments.NonDefaultSettings, loaded.Settings);
        Assert.Equal(3, loaded.Gestures.Count);
        Assert.Empty(_notices);
    }

    [Fact]
    public void LoadFallsBackToTheNewestBackupThatParses()
    {
        var store = NewStore();
        foreach (var name in new[] { "A", "B", "C" })
        {
            store.Save(SampleDocuments.WithGesture(name));
            _now = _now.AddSeconds(1);
        }

        File.WriteAllText(store.Location, "{ corrupt");
        File.WriteAllText(store.Backups.NewestFirst()[0], "{ \"schemaVersion\": 99 }");

        var loaded = store.Load();

        Assert.Equal("A", Assert.Single(loaded.Gestures).Name);
        Assert.Equal(3, _notices.Count);
        Assert.Contains("Could not load", _notices[0], StringComparison.Ordinal);
        Assert.Contains("newer", _notices[1], StringComparison.Ordinal);
        Assert.StartsWith("Loaded backup", _notices[2], StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFallsBackToDefaultsWithStartersWhenEverythingIsCorrupt()
    {
        var store = NewStore();
        store.Save(SampleDocuments.WithGesture("A"));
        store.Save(SampleDocuments.WithGesture("B"));
        File.WriteAllText(store.Location, "nope");
        File.WriteAllText(store.Backups.NewestFirst()[0], "nope");

        var loaded = store.Load();

        Assert.Same(ConfigDocument.Default, loaded);
        Assert.Equal(20, loaded.Gestures.Count);
        Assert.Contains(loaded.Gestures, gesture => gesture.Name == "Up");
        Assert.Contains(loaded.Gestures, gesture => gesture.Name == "Circle");
        Assert.Contains("defaults", _notices[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromAnEmptyFolderGivesDefaultsAndSaysSo()
    {
        var store = new FileConfigStore(_folder.File("missing"), _notices.Add);

        var loaded = store.Load();

        Assert.Equal(StarterGestures.All().Select(gesture => gesture.Id), loaded.Gestures.Select(gesture => gesture.Id));
        Assert.Equal(2, _notices.Count);
        Assert.Contains("No configuration", _notices[0], StringComparison.Ordinal);
    }

    [Fact]
    public void LocationIsAugramJsonInTheGivenFolder()
    {
        var store = NewStore();

        Assert.Equal(Path.Combine(_folder.Path, "augram.json"), store.Location);
        Assert.Equal(Path.Combine(_folder.Path, "backup"), store.Backups.Folder);
    }

    private FileConfigStore NewStore() => new(_folder.Path, _notices.Add, () => _now);
}
