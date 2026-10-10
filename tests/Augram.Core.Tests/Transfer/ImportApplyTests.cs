using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;
using static Augram.Core.Tests.Transfer.Support.TransferSamples;

namespace Augram.Core.Tests.Transfer;

/// <summary>
/// The rest of an import (plan 0003): a command's own steps follow its choice, the file's options only when asked and never
/// this machine's state, one undo step per store and a refusal when the stores moved, and the whole of it with Keep mine
/// everywhere equal to the sync's merge with no base.
/// </summary>
public sealed class ImportApplyTests
{
    [Fact]
    public void OwnStepsFollowTheirCommandWhenItDiffers()
    {
        var (mine, file) = CloseTabWithOwnStepsThere(sameOriginal: false);
        var plan = ImportPlan.Create(file, mine);

        var kept = plan.Preview;
        var taken = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs });
        var both = plan.Resolve(new ImportChoices { Default = SyncChoice.KeepBoth });

        Assert.Equal(SyncItemKind.Command, Assert.Single(plan.Conflicts).Kind);
        Assert.DoesNotContain(plan.Entries, entry => entry.Kind == SyncItemKind.CommandVersion);
        Assert.Null(CommandNamed(kept.Mapping, "Chrome", "Close tab").OwnVersion);
        Assert.Equal(HostPlatform.MacOS, CommandNamed(taken.Mapping, "Chrome", "Close tab").OwnVersion!.Platform);
        Assert.Null(CommandNamed(both.Mapping, "Chrome", "Close tab").OwnVersion);
        Assert.Equal("cmd+w", Assert.Single(CommandNamed(both.Mapping, "Chrome", "Close tab (2)").OwnVersion!.Steps).Step.Summary);
    }

    [Fact]
    public void OwnStepsThatAloneDiffer_AreAnItemOfTheirOwn_NewHere()
    {
        var (mine, file) = CloseTabWithOwnStepsThere(sameOriginal: true);

        var plan = ImportPlan.Create(file, mine);

        Assert.Empty(plan.Conflicts);
        Assert.Equal(ImportStatus.New, plan.Entries.Single(entry => entry.Kind == SyncItemKind.CommandVersion).Status);
        Assert.Equal("cmd+w", Assert.Single(CommandNamed(plan.Preview.Mapping, "Chrome", "Close tab").OwnVersion!.Steps).Step.Summary);
    }

    [Fact]
    public void TheFilesOptionsAreTakenOnlyWhenAsked_AndNeverThisMachinesState()
    {
        var theirs = SampleConfig();
        var mine = Empty() with { Settings = Settings.Default with { Sync = new SyncSettings(RepositoryUrl, Guid.NewGuid(), "MAC") } };

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine);
        var taken = plan.Resolve(new ImportChoices { TakeSettings = true });

        Assert.True(plan.HasSettings);
        Assert.True(plan.SettingsDiffer);
        Assert.False(plan.Preview.SettingsChanged);
        Assert.True(taken.SettingsChanged);
        var options = taken.Settings;
        Assert.Equal((MouseButton.Middle, IgnoreKeys.Control | IgnoreKeys.Alt), (options.General.StrokeButton, options.General.IgnoreKey));
        Assert.Equal((theirs.Settings.Capture, theirs.Settings.Trail, theirs.Settings.Recognition, theirs.Settings.NoMatch), (options.Capture, options.Trail, options.Recognition, options.NoMatch));
        Assert.Equal((false, true, false), (options.General.StartAtLogin, options.General.Enabled, options.General.ColourMenuBarIcon));
        Assert.Same(mine.Settings.Sync, options.Sync);
        Assert.False(ImportPlan.Create(Exported(theirs, ExportScope.GesturesOnly), mine).HasSettings);
    }

    [Fact]
    public void ApplyingIsOneUndoStepPerStoreThatChanged()
    {
        var mine = SampleConfig();
        var theirs = SampleConfig();
        theirs = WithGroup(theirs, "Chrome", chrome => chrome with { Commands = [.. chrome.Commands, NewCommand("Reload", GestureNamed(theirs.Gestures, "Left").Id, NewStep("f5"))] });
        theirs = theirs with { Gestures = [.. theirs.Gestures, Flick("Right")] };
        using var session = Session(mine);
        var before = Contents(session.Document);
        var result = ImportPlan.Create(Exported(theirs, ExportScope.Everything), session.Document).Resolve(new ImportChoices { TakeSettings = true });

        Assert.True(result.ApplyTo(session.Settings, session.Gestures, session.Mapping));

        Assert.Equal(Contents(result), Contents(session.Document));
        Assert.False(session.Settings.CanUndo);
        Assert.True(session.Gestures.Undo());
        Assert.False(session.Gestures.CanUndo);
        Assert.True(session.Mapping.Undo());
        Assert.False(session.Mapping.CanUndo);
        Assert.Equal(before, Contents(session.Document));
        Assert.True(session.HasPendingSave);
    }

    [Fact]
    public void ApplyingIsRefusedWhenAStoreMovedSinceThePlan_AndAPlanMadeAgainApplies()
    {
        var mine = SampleConfig();
        var theirs = SampleConfig() with { Gestures = [Flick("Right")] };
        using var session = Session(mine);
        var stale = ImportPlan.Create(Exported(theirs, ExportScope.GesturesOnly), session.Document).Preview;
        var mapping = session.Mapping.Current;

        session.Gestures.Rename(GestureNamed(session.Gestures.All, "Left").Id, "West");

        Assert.False(stale.ApplyTo(session.Settings, session.Gestures, session.Mapping));
        Assert.DoesNotContain(session.Gestures.All, gesture => gesture.Name == "Right");
        Assert.Same(mapping, session.Mapping.Current);
        Assert.True(ImportPlan.Create(Exported(theirs, ExportScope.GesturesOnly), session.Document).Preview.ApplyTo(session.Settings, session.Gestures, session.Mapping));
        Assert.Equal(["Up", "Down", "West", "Right"], session.Gestures.All.Select(gesture => gesture.Name));
    }

    [Fact]
    public void KeepMineEverywhere_IsTheSyncsMergeWithNoBase()
    {
        var mine = SampleConfig();
        var theirs = WithGroup(mine, "Chrome", chrome => chrome with { Commands = [.. chrome.Commands.Select(command => command with { Steps = [NewStep("ctrl+f4")] })] });
        theirs = theirs with
        {
            Gestures = [.. theirs.Gestures.Select(gesture => gesture.Name == "Left" ? gesture with { IsActive = false } : gesture), Flick("Right")],
            Mapping = MappingRules.ValidDocument(theirs.Mapping with { Groups = [.. theirs.Mapping.Groups, NewGroup("Firefox", null, NewCommand("Reload", GestureNamed(theirs.Gestures, "Up").Id, NewStep("f5")))] }),
        };
        var file = Exported(theirs, ExportScope.Everything);

        var plan = ImportPlan.Create(file, mine);
        var merged = ThreeWayMerge.Merge(SyncItemSet.Empty, SyncItemSet.From(mine.Gestures, mine.Mapping), SyncItemSet.From(file.Gestures, file.MappingOrEmpty));

        Assert.Equal(Contents(merged.Gestures, merged.Mapping), Contents(plan.Preview));
        Assert.Equal(merged.Conflicts.Select(conflict => conflict.Key.ToString()).Order(), plan.Conflicts.Select(conflict => conflict.Key.ToString()).Order());
        Assert.Equal(merged.Counts, plan.Preview.Counts);
        Assert.Equal(2, plan.Conflicts.Count);
    }

    /// <summary>Mine, and a file of Chrome whose Close tab has macOS steps of its own; its original steps changed too unless <paramref name="sameOriginal"/>.</summary>
    private static (ConfigDocument Mine, TransferFile File) CloseTabWithOwnStepsThere(bool sameOriginal)
    {
        var mine = SampleConfig();
        var theirs = WithGroup(mine, "Chrome", chrome => chrome with
        {
            Commands = [.. chrome.Commands.Select(command => (sameOriginal ? command : command with { Steps = [NewStep("ctrl+f4")] }) with
            {
                OwnVersion = new CommandVersion(HostPlatform.MacOS, [NewStep("cmd+w", HostPlatform.MacOS)], "0", DateTimeOffset.UnixEpoch),
            })],
        });
        return (mine, Exported(theirs, ExportScope.Of([GroupNamed(mine.Mapping, "Chrome").Id])));
    }
}
