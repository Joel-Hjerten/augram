using System.Globalization;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Screens;
using Augram.App.Sync;
using Augram.App.Tests.Support;
using Augram.App.Tests.Sync.Support;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Sync;
using Augram.Sync.Git;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>The sync slice of the composition root: what it registers, that it starts quietly with sync off and syncs once a repository is set, the headless app with sync off, Options › Sync rendered, and the health row.</summary>
public sealed class SyncModuleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "augram-sync-module-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp; the OS cleans it.
        }
    }

    [Fact]
    public void RegistersEverythingStartsQuietlyWithSyncOffAndSyncsOnceARepositoryIsSet()
    {
        var log = new ListEventLog();
        var schedule = new ManualSchedule();
        var services = Services(log, new SyncModuleOptions
        {
            ConfigFolder = _folder,
            Repository = folder => new FakeSyncRepository(new SyncRemote(), folder),
            StoreThread = (apply, _) => apply(),
            Schedule = schedule.Schedule,
            Marshal = action => action(),
        });

        using (var provider = services.BuildServiceProvider())
        {
            var repository = Assert.IsType<FakeSyncRepository>(provider.GetRequiredService<ISyncRepository>());
            Assert.Equal(Path.Combine(_folder, "sync", "repo"), repository.Folder);
            Assert.Equal(Path.Combine(_folder, "sync", "state"), provider.GetRequiredService<SyncBaseStore>().Folder);
            Assert.NotNull(provider.GetRequiredService<SyncCoordinator>());
            Assert.IsType<SyncJoinPresenter>(provider.GetRequiredService<ISyncJoinPresenter>());
            Assert.IsType<SyncConflictPresenter>(provider.GetRequiredService<ISyncConflictPresenter>());
            Assert.NotNull(provider.GetRequiredService<SyncViewModel>());
            var service = provider.GetRequiredService<SyncService>();
            Assert.False(service.IsConfigured);

            SyncModule.Start(provider);
            Thread.Sleep(150);
            Assert.True(log.Has(SyncService.LogSource, "Sync ready"));
            Assert.Equal(0, service.RunCount);
            Assert.Empty(repository.PreparedUrls);

            var settings = provider.GetRequiredService<SettingsStore>();
            settings.SetSync(settings.Current.Sync with { RepositoryUrl = SyncMachine.Url });
            SyncMachine.WaitFor(() => service.RunCount == 1, "the first sync");
            Assert.Equal(SyncStatus.UpToDate, service.LastReport!.Status);
            Assert.Equal([SyncMachine.Url], repository.PreparedUrls);
            var health = provider.GetRequiredService<HealthRegistry>().Current();
            Assert.Equal("UpToDate", health.LastSyncOutcome);
            Assert.Equal(service.LastReport.When, health.LastSyncAt);
        }

        Assert.True(File.Exists(Path.Combine(_folder, "sync", SyncFolders.MarkerFileName)));
    }

    [Fact]
    public void TheGitAdapterIsTheDefaultRepositoryOnTheCloneFolder()
    {
        var services = Services(new ListEventLog(), new SyncModuleOptions { ConfigFolder = _folder });

        using var provider = services.BuildServiceProvider();
        var repository = Assert.IsType<GitSyncRepository>(provider.GetRequiredService<ISyncRepository>());

        Assert.Equal(Path.GetFullPath(Path.Combine(_folder, "sync", "repo")), repository.Folder);
    }

    [AvaloniaFact]
    public void TheHeadlessAppBuildsWithSyncOffAndOptionsShowsTheSyncSection()
    {
        var services = TestAppBuilder.Services;
        var sync = services.GetRequiredService<SyncViewModel>();
        Assert.False(services.GetRequiredService<SyncService>().IsConfigured);

        var screen = Assert.IsType<FormScreen>(OptionsScreen.Declare(services.GetRequiredService<AppSettingsViewModel>(), sync));
        var form = new SectionForm { Screen = screen };
        new Window { Content = form, Width = 900, Height = 900 }.Show();

        // Sync is the last settings section; only About (three read-only rows) comes after it.
        Assert.Equal(OptionsSyncSection.Title, screen.Sections[^2].Title);
        Assert.Equal(OptionsScreen.AboutTitle, screen.Sections[^1].Title);
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToList();
        Assert.Equal(["Repository", "This machine", "Automatic sync", "Status", "Conflicts", "Details"], rows.SkipLast(3).TakeLast(6).Select(row => row.Label));
        Assert.Contains("stores no password", rows.Single(row => row.Label == "Repository").Help, StringComparison.Ordinal);
        Assert.False(rows.Single(row => row.Label == "Conflicts").IsVisible);
        Assert.False(rows.Single(row => row.Label == "Details").IsVisible);
        var status = rows.Single(row => row.Label == "Status").GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("note"));
        Assert.Equal(SyncViewModel.OffText, status.Text);
        Assert.False(rows.Single(row => row.Label == "Status").GetVisualDescendants().OfType<Button>().Single().IsEnabled);
    }

    [AvaloniaFact]
    public void TheTextEditorsReportDraftsAndCommitOnLeavingOrEnterAndRevertOnEscape()
    {
        var draft = "start";
        var problem = string.Empty;
        var commits = 0;
        var reverts = 0;
        var editor = OptionsSyncSection.CommittedText(
            new DelegateBinding<string>(() => draft, value => draft = value),
            new DelegateBinding<string>(() => problem),
            () => commits++,
            () => reverts++,
            wide: true);
        new Window { Content = editor }.Show();
        var box = editor.GetVisualDescendants().OfType<TextBox>().Single();
        var error = editor.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("error"));

        Assert.Equal("start", box.Text);
        Assert.Contains("wide", box.Classes);
        Assert.False(error.IsVisible);
        box.Text = "typed";
        Assert.Equal("typed", draft);
        Assert.Equal(0, commits);

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(1, commits);
        box.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        Assert.Equal(2, commits);
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Equal(1, reverts);
    }

    [Fact]
    public void HealthShowsTheLastSync()
    {
        var registry = new HealthRegistry();
        var at = new DateTimeOffset(2026, 10, 7, 14, 32, 5, TimeSpan.Zero);
        var vm = new HealthViewModel(registry, TimeSpan.Zero);
        Assert.Equal("–", vm.LastSync);

        registry.Register(snapshot => snapshot with { LastSyncOutcome = "Failed", LastSyncAt = at });
        vm.Refresh();

        Assert.Equal($"Failed at {at.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)}", vm.LastSync);
    }

    private ServiceCollection Services(ListEventLog log, SyncModuleOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventLog>(log);
        services.AddSingleton<HealthRegistry>();
        services.AddSingleton<RecognitionLog>();
        services.AddSingleton(TestBuilds.Release);
        services.AddSingleton<AppState>();
        EngineModule.Register(services, new EngineModuleOptions
        {
            ConfigFolder = _folder,
            InputSource = _ => new FakeInputSource(),
            PlatformAdapters = false,
            Marshal = action => action(),
        });
        SyncModule.Register(services, options);
        return services;
    }
}
