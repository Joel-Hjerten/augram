using Augram.Core.Config;
using Xunit;

namespace Augram.Core.Tests.Config;

/// <summary>
/// The <c>appearance</c> section (plan 0006 step 3): the plan's defaults, a round trip, an additive section (missing, null or
/// partial reads the defaults, as <c>sync</c> does), its rules and <see cref="SettingsStore.SetAppearance"/>.
/// </summary>
public sealed class AppearanceSettingsTests
{
    private static readonly AppearanceSettings Picked = new()
    {
        Theme = AppTheme.System,
        WindowBackground = WindowBackground.Solid,
        TintPercent = 0,
        CornerRadiusPx = 18,
        AccentFollowsTrail = false,
        Accent = new RgbColor(54, 214, 231),
    };

    [Fact]
    public void TheDefaultsAreThePlans_DarkFrostedGlass62PercentTint12PxCorners_AccentFollowingTheTrail()
    {
        var appearance = AppearanceSettings.Default;

        Assert.Equal(AppTheme.Dark, appearance.Theme);
        Assert.Equal(WindowBackground.FrostedGlass, appearance.WindowBackground);
        Assert.Equal(62, appearance.TintPercent);
        Assert.Equal(12, appearance.CornerRadiusPx);
        Assert.True(appearance.AccentFollowsTrail);
        Assert.Equal(new RgbColor(245, 197, 66), appearance.Accent);
        Assert.Equal(appearance, Settings.Default.Appearance);
        Assert.Equal(appearance, new AppearanceSettings());
    }

    [Fact]
    public void TheAppearanceSectionRoundTrips_EnumsAsNames_TheAccentAsHex()
    {
        var document = new ConfigDocument { Settings = Settings.Default with { Appearance = Picked } };

        var json = ConfigSerializer.Write(document);
        var back = ConfigSerializer.Read(json);

        Assert.Equal(Picked, back.Settings.Appearance);
        Assert.Contains("\"appearance\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"System\"", json, StringComparison.Ordinal);
        Assert.Contains("\"windowBackground\": \"Solid\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tintPercent\": 0", json, StringComparison.Ordinal);
        Assert.Contains("\"cornerRadiusPx\": 18", json, StringComparison.Ordinal);
        Assert.Contains("\"accentFollowsTrail\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"accent\": \"#36D6E7\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"schemaVersion\": 7, \"settings\": { \"trail\": { \"widthPx\": 9 } } }")]
    [InlineData("{ \"schemaVersion\": 6, \"settings\": { \"general\": { \"strokeButton\": \"Middle\" } } }")]
    [InlineData("{ \"schemaVersion\": 1, \"settings\": {} }")]
    [InlineData("{ \"schemaVersion\": 7, \"settings\": { \"appearance\": null } }")]
    [InlineData("{ \"schemaVersion\": 7, \"settings\": { \"appearance\": {} } }")]
    public void AFileWithoutTheSection_OrWithAnEmptyOne_ReadsTheDefaults(string json)
    {
        var back = ConfigSerializer.Read(json);

        Assert.Equal(AppearanceSettings.Default, back.Settings.Appearance);
    }

    [Fact]
    public void APartialSectionFillsInTheRest_AndUnknownMembersAreSkipped()
    {
        var back = ConfigSerializer.Read(
            "{ \"schemaVersion\": 7, \"settings\": { \"appearance\": { \"theme\": \"light\", \"tintPercent\": 80, \"frost\": 3 } } }");

        Assert.Equal(AppearanceSettings.Default with { Theme = AppTheme.Light, TintPercent = 80 }, back.Settings.Appearance);
    }

    [Theory]
    [InlineData("{ \"theme\": \"Neon\" }", "'theme' must be one of Dark, Light, System.")]
    [InlineData("{ \"windowBackground\": 1 }", "'windowBackground' must be one of FrostedGlass, WallpaperTint, Solid.")]
    [InlineData("{ \"tintPercent\": \"high\" }", "'tintPercent' must be a whole number.")]
    [InlineData("{ \"cornerRadiusPx\": 12.5 }", "'cornerRadiusPx' must be a whole number.")]
    [InlineData("{ \"accentFollowsTrail\": \"yes\" }", "'accentFollowsTrail' must be true or false.")]
    [InlineData("{ \"accent\": \"yellow\" }", "'accent' must be a \"#RRGGBB\" string.")]
    [InlineData("[]", "'appearance' must be an object.")]
    public void AMemberOfTheWrongShapeIsAFormatErrorNamingIt(string section, string message)
    {
        var json = $"{{ \"schemaVersion\": 7, \"settings\": {{ \"appearance\": {section} }} }}";

        var ex = Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Read(json));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 12)]
    [InlineData(101, 12)]
    [InlineData(62, 3)]
    [InlineData(62, 19)]
    public void OutOfRangeTintOrCornerRoundingIsRejectedAndNotCommitted(int tint, int radius)
    {
        var store = new SettingsStore(Settings.Default);

        var ex = Assert.Throws<SettingsValidationException>(
            () => store.SetAppearance(AppearanceSettings.Default with { TintPercent = tint, CornerRadiusPx = radius }));

        Assert.Matches("^(Tint must be between 0 and 100 %|Corner rounding must be between 4 and 18 px)\\.$", ex.Message);
        Assert.Equal(AppearanceSettings.Default, store.Current.Appearance);
        Assert.False(store.CanUndo);
        Assert.Equal(0, store.Version);
    }

    [Fact]
    public void TheEndsOfEachRangeAreAccepted()
    {
        var store = new SettingsStore(Settings.Default);

        store.SetAppearance(AppearanceSettings.Default with { TintPercent = 0, CornerRadiusPx = 4 });
        store.SetAppearance(AppearanceSettings.Default with { TintPercent = 100, CornerRadiusPx = 18 });

        Assert.Equal((100, 18), (store.Current.Appearance.TintPercent, store.Current.Appearance.CornerRadiusPx));
    }

    [Fact]
    public void AnUndefinedThemeOrWindowBackgroundIsRejected()
    {
        var store = new SettingsStore(Settings.Default);

        Assert.Throws<SettingsValidationException>(() => store.SetAppearance(AppearanceSettings.Default with { Theme = (AppTheme)7 }));
        Assert.Throws<SettingsValidationException>(() => store.SetAppearance(AppearanceSettings.Default with { WindowBackground = (WindowBackground)9 }));
        Assert.Equal(AppearanceSettings.Default, store.Current.Appearance);
    }

    [Fact]
    public void SetAppearanceReplacesTheSection_AsOneUndoStep()
    {
        var store = new SettingsStore(Settings.Default);
        int raised = 0;
        store.Changed += (_, _) => raised++;

        var result = store.SetAppearance(Picked);

        Assert.Same(result, store.Current);
        Assert.Equal(Picked, store.Current.Appearance);
        Assert.Equal(Settings.Default.Trail, store.Current.Trail);
        Assert.Equal((1, 1), (raised, store.Version));
        Assert.True(store.Undo());
        Assert.Equal(AppearanceSettings.Default, store.Current.Appearance);
    }

    [Fact]
    public void AnOutOfRangeValueInTheFile_ResetsTheSettingsOnLoad_WithANotice()
    {
        var notices = new List<string>();
        var loaded = ConfigSerializer.Read("{ \"schemaVersion\": 7, \"settings\": { \"appearance\": { \"tintPercent\": 150 } } }");

        using var session = new ConfigSession(new InMemoryConfigStore(loaded), new ManualScheduler().Schedule, notices.Add);

        Assert.Equal(150, loaded.Settings.Appearance.TintPercent);
        Assert.Equal(Settings.Default, session.Settings.Current);
        Assert.Contains("Tint must be between 0 and 100 %.", Assert.Single(notices), StringComparison.Ordinal);
    }
}
