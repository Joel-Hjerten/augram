using Augram.Core.Config;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class SyncSettingsTests
{
    private static readonly Guid MachineId = new("6d1e7f3a-0000-4000-8000-0000000000aa");

    [Fact]
    public void TheSyncSectionRoundTrips()
    {
        var sync = new SyncSettings("git@github.com:joel/augram-settings.git", MachineId, "PC-HOME", AutoSync: false);
        var document = new ConfigDocument { Settings = Settings.Default with { Sync = sync } };

        var json = ConfigSerializer.Write(document);
        var back = ConfigSerializer.Read(json);

        Assert.Equal(sync, back.Settings.Sync);
        Assert.Contains("\"sync\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"repositoryUrl\": \"git@github.com:joel/augram-settings.git\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"machineId\": \"{MachineId}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"machineName\": \"PC-HOME\"", json, StringComparison.Ordinal);
        Assert.Contains("\"autoSync\": false", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isOn", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingSectionOrMemberTakesItsDefault()
    {
        var bare = ConfigSerializer.Read("{ \"schemaVersion\": 1, \"settings\": {} }");
        var partial = ConfigSerializer.Read("{ \"schemaVersion\": 1, \"settings\": { \"sync\": { \"repositoryUrl\": \"https://github.com/joel/a.git\" } } }");

        Assert.Equal(SyncSettings.Default, bare.Settings.Sync);
        Assert.Null(bare.Settings.Sync.RepositoryUrl);
        Assert.False(bare.Settings.Sync.IsOn);
        Assert.Equal(Guid.Empty, bare.Settings.Sync.MachineId);
        Assert.Equal(Environment.MachineName, bare.Settings.Sync.MachineName);
        Assert.True(bare.Settings.Sync.AutoSync);
        Assert.Equal("https://github.com/joel/a.git", partial.Settings.Sync.RepositoryUrl);
        Assert.Equal(Environment.MachineName, partial.Settings.Sync.MachineName);
        Assert.True(partial.Settings.Sync.AutoSync);
    }

    [Fact]
    public void TheMachineIdIsGeneratedOnceAndSurvivesASaveAndReload()
    {
        var store = new InMemoryConfigStore(new ConfigDocument());
        var scheduler = new ManualScheduler();
        Guid id;
        using (var session = new ConfigSession(store, scheduler.Schedule))
        {
            session.Settings.SetNoMatch(NoMatchBehaviour.ReplayClick);
            id = session.Settings.EnsureMachineId();

            Assert.NotEqual(Guid.Empty, id);
            Assert.Equal(id, session.Settings.EnsureMachineId());
            Assert.False(session.Settings.CanUndo);
            scheduler.RunPending();
        }

        using var reopened = new ConfigSession(new InMemoryConfigStore(store.Saved[^1]), scheduler.Schedule);
        Assert.Equal(id, reopened.Settings.EnsureMachineId());
        Assert.Equal(NoMatchBehaviour.ReplayClick, reopened.Settings.Current.NoMatch);
    }

    [Theory]
    [InlineData("https://joel:ghp_secret@github.com/joel/augram-settings.git")]
    [InlineData("https://joel@github.com/joel/augram-settings.git")]
    public void AUrlWithAUserNameOrTokenIsRefusedAndSaysWhy(string url)
    {
        var store = new SettingsStore(Settings.Default);

        var ex = Assert.Throws<SettingsValidationException>(() => store.SetSync(new SyncSettings(url, MachineId, "PC")));

        Assert.Equal(SyncSettingsRules.UserInfoProblem, ex.Message);
        Assert.Contains("credential helper", ex.Message, StringComparison.Ordinal);
        Assert.Null(store.Current.Sync.RepositoryUrl);
        Assert.False(store.CanUndo);
    }

    [Theory]
    [InlineData("https://github.com/joel/augram-settings.git")]
    [InlineData("git@github.com:joel/augram-settings.git")]
    [InlineData("file:///srv/augram-settings.git")]
    public void HttpsScpAndFileUrlsAreAccepted(string url) => Assert.Null(SyncSettingsRules.UrlProblem(url));

    [Fact]
    public void AFullLocalPathIsAccepted() => Assert.Null(SyncSettingsRules.UrlProblem(Path.Combine(Path.GetTempPath(), "augram-settings")));

    [Theory]
    [InlineData("http://github.com/joel/a.git", "https://")]
    [InlineData("ssh://git@github.com/joel/a.git", "git@host:path")]
    [InlineData("git@github.com", "looks like")]
    [InlineData("github.com/joel/a.git", "full path")]
    [InlineData("   ", "clear it")]
    public void OtherUrlsAreRefusedWithAHint(string url, string hint)
        => Assert.Contains(hint, SyncSettingsRules.UrlProblem(url), StringComparison.Ordinal);

    [Fact]
    public void SetSyncTrimsTurnsABlankUrlOffAndKeepsTheMachineId()
    {
        var store = new SettingsStore(Settings.Default with { Sync = new SyncSettings("https://github.com/joel/a.git", MachineId, "PC") });

        store.SetSync(new SyncSettings("   ", Guid.Empty, "  PC-HOME  "));

        Assert.Null(store.Current.Sync.RepositoryUrl);
        Assert.False(store.Current.Sync.IsOn);
        Assert.Equal("PC-HOME", store.Current.Sync.MachineName);
        Assert.Equal(MachineId, store.Current.Sync.MachineId);
    }

    [Fact]
    public void ABlankMachineNameIsRefused()
    {
        var store = new SettingsStore(Settings.Default);

        Assert.Throws<SettingsValidationException>(() => store.Apply(settings => settings with { Sync = settings.Sync with { MachineName = " " } }));
    }

    [Theory]
    [InlineData("https://joel:token@github.com/joel/a.git", "github.com")]
    [InlineData("git@gitlab.com:joel/a.git", "gitlab.com")]
    [InlineData("file:///srv/a.git", SyncSettingsRules.LocalHost)]
    [InlineData(null, "none")]
    public void TheHostIsAllTheLogMaySay(string? url, string host) => Assert.Equal(host, SyncSettingsRules.Host(url));
}
