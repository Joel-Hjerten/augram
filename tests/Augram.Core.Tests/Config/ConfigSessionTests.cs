using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Tests.Fixtures;
using Augram.Core.Tests.Mapping.Support;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class ConfigSessionTests
{
    private readonly ManualScheduler _scheduler = new();
    private readonly List<string> _notices = [];

    [Fact]
    public void LoadsBothStoresFromTheDocument()
    {
        var store = new InMemoryConfigStore(SampleDocuments.Full());

        using var session = Open(store);

        Assert.Equal(SampleDocuments.NonDefaultSettings, session.Settings.Current);
        Assert.Equal(3, session.Gestures.All.Count);
        Assert.Equal(SampleDocuments.NonDefaultSettings, session.Document.Settings);
        Assert.Empty(store.Saved);
        Assert.Empty(_notices);
    }

    [Fact]
    public void TwoChangesWithinTheDelayProduceOneSaveOfTheWholeDocument()
    {
        var store = new InMemoryConfigStore(new ConfigDocument());
        using var session = Open(store);
        int changed = 0;
        session.DocumentChanged += (_, _) => changed++;

        session.Settings.SetNoMatch(NoMatchBehaviour.ReplayClick);
        var added = session.Gestures.Add(TestGestures.Create("Up", StockFlicks.Template("Up")));

        Assert.Empty(store.Saved);
        Assert.True(session.HasPendingSave);
        Assert.Equal(2, changed);
        Assert.Equal(2, _scheduler.Scheduled);
        Assert.Equal(1, _scheduler.PendingCount);

        _scheduler.RunPending();

        var saved = Assert.Single(store.Saved);
        Assert.Equal(NoMatchBehaviour.ReplayClick, saved.Settings.NoMatch);
        Assert.Equal(added.Id, Assert.Single(saved.Gestures).Id);
        Assert.False(session.HasPendingSave);
    }

    [Fact]
    public void FlushSavesImmediatelyAndCancelsTheScheduledSave()
    {
        var store = new InMemoryConfigStore(new ConfigDocument());
        using var session = Open(store);
        session.Settings.SetNoMatch(NoMatchBehaviour.ReplayClick);

        session.Flush();

        Assert.Single(store.Saved);
        Assert.Equal(0, _scheduler.PendingCount);
        _scheduler.RunPending();
        Assert.Single(store.Saved);

        session.Flush();
        Assert.Single(store.Saved);
    }

    [Fact]
    public void UndoIsAChangeAndIsSavedToo()
    {
        var store = new InMemoryConfigStore(new ConfigDocument());
        using var session = Open(store);
        session.Settings.SetNoMatch(NoMatchBehaviour.ReplayClick);
        _scheduler.RunPending();

        session.Settings.Undo();
        _scheduler.RunPending();

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(NoMatchBehaviour.DoNothing, store.Saved[^1].Settings.NoMatch);
    }

    [Fact]
    public void DisposeFlushesAndStopsListening()
    {
        var store = new InMemoryConfigStore(new ConfigDocument());
        var session = Open(store);
        session.Settings.SetNoMatch(NoMatchBehaviour.ReplayClick);

        session.Dispose();
        session.Settings.SetNoMatch(NoMatchBehaviour.DoNothing);

        Assert.Single(store.Saved);
        Assert.Equal(0, _scheduler.PendingCount);
    }

    [Fact]
    public void AFailedBackgroundSaveIsReportedAndStaysPending()
    {
        var store = new InMemoryConfigStore(new ConfigDocument()) { SaveFailure = new IOException("disk full") };
        using var session = Open(store);
        session.Settings.SetNoMatch(NoMatchBehaviour.ReplayClick);

        _scheduler.RunPending();

        Assert.True(session.HasPendingSave);
        Assert.Contains("disk full", Assert.Single(_notices), StringComparison.Ordinal);
        store.SaveFailure = null;
        session.Flush();
        Assert.Single(store.Saved);
    }

    [Fact]
    public void InvalidSavedGesturesAreSkippedWithANoticeAndTheRestKept()
    {
        var up = StockFlicks.Template("Up");
        var document = new ConfigDocument
        {
            Gestures =
            [
                TestGestures.Create("Up", up),
                TestGestures.Create(" up ", up),
                TestGestures.Create("Down", StockFlicks.Template("Down")),
            ],
        };

        using var session = Open(new InMemoryConfigStore(document));

        Assert.Equal(["Up", "Down"], session.Gestures.All.Select(gesture => gesture.Name));
        Assert.False(session.Gestures.CanUndo);
        Assert.Contains("skipped", Assert.Single(_notices), StringComparison.Ordinal);
    }

    [Fact]
    public void OutOfRangeSavedSettingsFallBackToDefaultsWithANotice()
    {
        var document = new ConfigDocument { Settings = Settings.Default with { Trail = new TrailSettings { Opacity = 7 } } };

        using var session = Open(new InMemoryConfigStore(document));

        Assert.Equal(Settings.Default, session.Settings.Current);
        Assert.Contains("reset to defaults", Assert.Single(_notices), StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingChangeIsSavedWithTheWholeDocument()
    {
        var store = new InMemoryConfigStore(new ConfigDocument());
        using var session = Open(store);
        int changed = 0;
        session.DocumentChanged += (_, _) => changed++;

        var chrome = session.Mapping.AddGroup(MappingFixtures.NewGroup("Chrome"));
        _scheduler.RunPending();

        Assert.Equal(1, changed);
        var saved = Assert.Single(store.Saved);
        Assert.Equal(chrome.Id, saved.Mapping.Groups[1].Id);
        Assert.Same(session.Mapping.Current, session.Document.Mapping);
    }

    [Fact]
    public void AnInvalidSavedMappingLoadsWhatPassesWithNoticesAndNoHistory()
    {
        var up = GestureId.New();
        var mapping = new MappingDocument(
            [
                MappingFixtures.NewGroup("Chrome", commands: [MappingFixtures.NewCommand("Close tab", up), MappingFixtures.NewCommand("Also close", up)]),
                MappingFixtures.NewGlobal(MappingFixtures.NewCommand("Close", up)),
                MappingFixtures.NewGroup("chrome"),
            ],
            [new IgnoredApp(GroupId.New(), " ", true, MappingFixtures.ByProcess("x.exe"), false)]);

        using var session = Open(new InMemoryConfigStore(new ConfigDocument { Mapping = mapping }));

        Assert.Equal(["Global", "Chrome"], session.Mapping.Current.Groups.Select(group => group.Name));
        Assert.Equal("Close", Assert.Single(session.Mapping.Global.Commands).Name);
        Assert.Equal("Close tab", Assert.Single(session.Mapping.Current.Groups[1].Commands).Name);
        Assert.Empty(session.Mapping.Current.Ignored);
        Assert.False(session.Mapping.CanUndo);
        Assert.Equal(3, _notices.Count);
        Assert.All(_notices, notice => Assert.Contains("skipped", notice, StringComparison.Ordinal));
    }

    [Fact]
    public void ADuplicateSavedCategoryCostsOnlyItselfAndItsCommandsLoadUncategorized()
    {
        var media = MappingFixtures.NewCategory("Media");
        var twin = MappingFixtures.NewCategory("media");
        var global = MappingFixtures.NewGlobal(
            MappingFixtures.NewCommand("Play").In(media),
            MappingFixtures.NewCommand("Pause").In(twin)) with { Categories = [media, twin] };

        using var session = Open(new InMemoryConfigStore(new ConfigDocument { Mapping = new MappingDocument([global], []) }));

        Assert.Equal(media, Assert.Single(session.Mapping.Global.Categories));
        Assert.Equal([null, media.Id], session.Mapping.Global.Commands.Select(command => command.CategoryId));
        Assert.Equal($"Category 'media' ({twin.Id}) in 'Global' skipped: A category named 'Media' already exists in 'Global'.", Assert.Single(_notices));
        Assert.False(session.Mapping.CanUndo);
    }

    private ConfigSession Open(IConfigStore store) => new(store, _scheduler.Schedule, _notices.Add);
}
