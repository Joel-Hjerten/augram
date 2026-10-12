using System.Globalization;
using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Components.SyncConflictList;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Sync;
using Augram.Core.Text;

namespace Augram.App.Sync;

/// <summary>
/// One version of a conflicting sync item in words, for the conflict list (F8 sync, conflicts; the Augram file
/// import's differing items too): a gesture as its glyph and "Zig · 3 samples", anything else as a one-line summary,
/// "deleted" for a side that has none, and a note when this build cannot read the version. Names a command's
/// summary needs (its group, hold remap and gesture) are read from the stores' snapshots it is given; what an app group or an
/// exclusion matches is worded for the platform it is given (<see cref="AppMatcher.Summary"/>, the Exclusions row's words).
/// </summary>
public sealed class SyncConflictSideText
{
    private readonly IReadOnlyList<Gesture> _gestures;
    private readonly MappingDocument _mapping;
    private readonly StepRegistry _steps;
    private readonly HostPlatform _platform;

    public SyncConflictSideText(IReadOnlyList<Gesture> gestures, MappingDocument mapping, StepRegistry steps, HostPlatform platform)
    {
        _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _platform = platform;
    }

    /// <summary>The side for one version of the conflict's item: <paramref name="content"/> is its JSON, null when that machine deleted it.</summary>
    public SyncConflictSide Side(SyncConflict conflict, string? content)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        if (content is null)
        {
            return SyncConflictSide.Deleted;
        }

        SyncItem item;
        try
        {
            item = SyncItem.Parse(conflict.Key, content, _steps);
        }
        catch (ConfigFormatException)
        {
            return new SyncConflictSide("(this version cannot be read here)");
        }

        return item switch
        {
            SyncItem.GestureItem gesture => new SyncConflictSide(GestureText(gesture.Gesture), gesture.Gesture.Samples.Count > 0 ? gesture.Gesture.Samples[0] : null),
            SyncItem.CommandItem command => new SyncConflictSide(CommandText(command.GroupId, command.Command)),
            SyncItem.GroupItem group => new SyncConflictSide(GroupText(group.Header)),
            SyncItem.CategoryItem category => new SyncConflictSide(CategoryText(category.Category)),
            SyncItem.HoldRemapItem holdRemap => new SyncConflictSide(HoldRemapText(holdRemap.HoldRemap)),
            SyncItem.IgnoredItem ignored => new SyncConflictSide(IgnoredText(ignored.App)),
            SyncItem.VersionItem version => new SyncConflictSide(VersionText(version.Version)),
            _ => new SyncConflictSide(item.Name),
        };
    }

    private static string GestureText(Gesture gesture)
    {
        var samples = Sentence.Count(gesture.Samples.Count, "sample");
        return $"{gesture.Name} · {samples}{(gesture.IsActive ? string.Empty : " · inactive")}";
    }

    /// <summary>
    /// "Chrome › Close tab: gesture Zig → Wait 30 ms", "Blender › Space › Orbit: Left → …", "Global › Media › Mute: …"
    /// (<see cref="CommandNames.Label(string, string?, string)"/>, the parent from <see cref="CommandNames.Parent"/>).
    /// </summary>
    private string CommandText(GroupId groupId, Command command)
    {
        var group = _mapping.Groups.FirstOrDefault(candidate => candidate.Id == groupId);
        var parent = command.HoldRemapId is not null && group?.HoldRemapOf(command) is null
            ? "another hold remap"
            : group is null ? null : CommandNames.Parent(group, command);
        var label = CommandNames.Label(group?.Name ?? "another group", parent, command.Name);
        var trigger = TriggerText(command.Trigger);
        var steps = command.Steps.Count == 0 ? "no steps" : string.Join(", ", command.Steps.Select(step => step.Step.Summary));
        return $"{label}: {trigger} → {steps}{(command.IsActive ? string.Empty : " (inactive)")}";
    }

    /// <summary>"gesture Undo", "Shift + gesture Undo", "Right + wheel up", "a gesture not on this machine".</summary>
    private string TriggerText(Trigger trigger)
        => trigger is Trigger.GestureTrigger gesture
            ? TriggerKindExtensions.Text(
                trigger,
                _gestures.FirstOrDefault(candidate => candidate.Id == gesture.GestureId) is { } known ? $"gesture {known.Name}" : "a gesture not on this machine",
                HotkeyText.Names)
            : trigger.Describe();

    private string VersionText(CommandVersion version)
    {
        var platform = version.Platform.DisplayName();
        var steps = version.Steps.Count == 0 ? "no steps (does nothing there)" : string.Join(", ", version.Steps.Select(step => step.Step.SummaryOn(step.AuthoredOn)));
        return version.Trigger is { } own ? $"{platform} trigger: {TriggerText(own)}; {platform} steps: {steps}" : $"{platform} steps: {steps}";
    }

    /// <summary>"Chrome: matches chrome.exe", "matches Google Chrome (guessed)", "matches nothing yet", "matches nothing on macOS".</summary>
    private string GroupText(AppGroup group)
    {
        var summary = (group.Matcher ?? AppMatcher.Empty).Summary(_platform);
        var matches = summary.IsEmpty ? summary.Text : "matches " + summary.Text;
        return $"{group.Name}: {matches}{(group.SuppressGlobals ? ", suppresses global commands" : string.Empty)}{(group.IsActive ? string.Empty : " (inactive)")}";
    }

    /// <summary>"Personal", or "Personal · Windows only" when its Use on (F8, 2026-10-08) is what differs.</summary>
    private static string CategoryText(CommandCategory category)
        => category.UseOn == PlatformSet.All ? category.Name : $"{category.Name} · {StepPlatformMarker.Only(category.UseOn)}";

    /// <summary>"Space: hold Space, tap 180 ms", then " · Windows only" and " (inactive)" when they apply (plan 0002).</summary>
    private static string HoldRemapText(HoldRemap holdRemap)
    {
        var key = holdRemap.HoldKey == KeyCode.None ? "no hold key yet" : $"hold {HotkeyText.KeyName(holdRemap.HoldKey)}";
        var useOn = holdRemap.UseOn == PlatformSet.All ? string.Empty : $" · {StepPlatformMarker.Only(holdRemap.UseOn)}";
        return string.Create(CultureInfo.InvariantCulture, $"{holdRemap.Name}: {key}, tap {holdRemap.TapTimeMs} ms{useOn}{(holdRemap.IsActive ? string.Empty : " (inactive)")}");
    }

    /// <summary>"Game: game.exe, turns Augram off": the Exclusions row's summary of what it matches.</summary>
    private string IgnoredText(IgnoredApp app)
    {
        var matches = app.Matcher.Summary(_platform).Text;
        return $"{app.Name}: {matches}{(app.DisableEntirely ? ", turns Augram off" : string.Empty)}{(app.IsActive ? string.Empty : " (inactive)")}";
    }
}
