using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>
/// What survives a loaded file that breaks a rule (checklist A3: the app always starts): each aggregate is taken strictly
/// first, and only when that fails is it rebuilt item by item, keeping what passes and reporting the rest through
/// <c>notice</c>, one line each; the rebuilt store's undo history is cleared. <see cref="ConfigSession"/> asks it once, at
/// load. The sync and an import refuse a whole file instead; this tolerance is the running app's own.
/// </summary>
internal static class ConfigRecovery
{
    /// <summary>The settings as loaded, or <see cref="Settings.Default"/> with a notice when they break a rule.</summary>
    public static Settings ValidSettings(Settings settings, Action<string>? notice)
    {
        try
        {
            SettingsRules.EnsureValid(settings);
            return settings;
        }
        catch (SettingsValidationException ex)
        {
            notice?.Invoke($"Settings reset to defaults: {ex.Message}");
            return Settings.Default;
        }
    }

    /// <summary>Strict first; if the saved set breaks a rule, keep every gesture that passes on its own and report the rest.</summary>
    public static GestureLibrary ValidLibrary(IReadOnlyList<Gesture> gestures, Action<string>? notice)
    {
        try
        {
            return new GestureLibrary(gestures);
        }
        catch (GestureValidationException)
        {
            var library = new GestureLibrary();
            foreach (var gesture in gestures)
            {
                try
                {
                    library.Add(gesture);
                }
                catch (GestureValidationException ex)
                {
                    notice?.Invoke($"Gesture '{gesture.Name}' ({gesture.Id}) skipped: {ex.Message}");
                }
            }

            library.ClearHistory();
            return library;
        }
    }

    /// <summary>Strict first; if the saved mapping breaks a rule, load group by group and command by command, keep what passes and report the rest.</summary>
    public static MappingStore ValidMapping(MappingDocument mapping, Action<string>? notice)
    {
        try
        {
            return new MappingStore(mapping);
        }
        catch (MappingValidationException)
        {
            var store = new MappingStore();
            bool globalLoaded = false;
            foreach (var group in mapping.Groups)
            {
                if (group.IsGlobal && globalLoaded)
                {
                    notice?.Invoke($"App group '{group.Name}' ({group.Id}) skipped: only one Global group is allowed.");
                    continue;
                }

                globalLoaded |= group.IsGlobal;
                LoadGroup(store, group, notice);
            }

            foreach (var app in mapping.Ignored)
            {
                Try(() => store.AddIgnored(app), $"Excluded app '{app.Name}' ({app.Id})", notice);
            }

            RestoreIgnoredReferences(store, mapping, notice);
            store.ClearHistory();
            return store;
        }
    }

    /// <summary>
    /// The group without its commands, categories and hold remaps first, then each category, each hold remap, each command: a
    /// bad category costs only itself (its commands load without it: on Global, in Uncategorized), and so does a bad hold remap (its commands load as
    /// ordinary commands without their input).
    /// </summary>
    private static void LoadGroup(MappingStore store, AppGroup group, Action<string>? notice)
    {
        var shell = group with { Commands = [], Categories = [], HoldRemaps = [] };
        if (!Try(() => _ = group.IsGlobal ? store.UpdateGroup(shell) : store.AddGroup(shell), $"App group '{group.Name}' ({group.Id})", notice))
        {
            return;
        }

        foreach (var category in group.Categories)
        {
            Try(() => AddCategory(store, group.Id, category), $"Category '{category.Name}' ({category.Id}) in '{group.Name}'", notice);
        }

        foreach (var holdRemap in group.HoldRemaps)
        {
            Try(() => store.AddHoldRemap(group.Id, holdRemap), $"Hold remap '{holdRemap.Name}' ({holdRemap.Id}) in '{group.Name}'", notice);
        }

        foreach (var command in group.Commands)
        {
            Try(() => store.AddCommand(group.Id, command), $"Command '{command.Name}' ({command.Id}) in '{group.Name}'", notice);
        }
    }

    /// <summary>
    /// The "Not in" (plan 0004) and "Also in" (plan 0005) of every command that loaded: they name ignored apps (Ignored › Per
    /// command entries, plain Exclusions › Global entries), which load after every group, so the store dropped those ids then.
    /// Now that the ignored apps are in, both lists are put back on the command loaded from that group; an entry that did not
    /// load stays out.
    /// </summary>
    private static void RestoreIgnoredReferences(MappingStore store, MappingDocument mapping, Action<string>? notice)
    {
        foreach (var (group, command) in mapping.AllCommands().Where(pair => pair.Command.NotIn.Count > 0 || pair.Command.AlsoIn.Count > 0))
        {
            if (store.FindCommand(command.Id) is { } loaded && loaded.Group.Id == group.Id)
            {
                var lists = (command.NotIn.Count > 0, command.AlsoIn.Count > 0) switch
                {
                    (true, true) => "'Not in' and 'Also in'",
                    (true, false) => "'Not in'",
                    _ => "'Also in'",
                };
                Try(
                    () => store.UpdateCommand(group.Id, loaded.Command with { NotIn = command.NotIn, AlsoIn = command.AlsoIn }),
                    $"The {lists} of command '{command.Name}' ({command.Id}) in '{group.Name}'",
                    notice);
            }
        }
    }

    private static void AddCategory(MappingStore store, GroupId groupId, CommandCategory category)
    {
        var group = store.FindGroup(groupId)!;
        store.UpdateGroup(group with { Categories = [.. group.Categories, category] });
    }

    private static bool Try(Action load, string what, Action<string>? notice)
    {
        try
        {
            load();
            return true;
        }
        catch (MappingValidationException ex)
        {
            notice?.Invoke($"{what} skipped: {ex.Message}");
            return false;
        }
    }
}
