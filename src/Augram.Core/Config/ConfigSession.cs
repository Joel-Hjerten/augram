using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>
/// Owns the store, the loaded document and the three aggregate stores for the app's lifetime,
/// and implements live save (checklist A3): any change to any store schedules one debounced
/// write of the whole document <see cref="SaveDelay"/> later; a burst of edits is one file
/// write and one backup. The scheduler is injected so the host runs the save on its UI thread
/// (same thread as the edits; this type is single-writer) and tests run it by hand. Other
/// threads (the engine) read each store's current snapshot after <see cref="DocumentChanged"/>.
/// </summary>
public sealed class ConfigSession : IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(250);

    private readonly Func<Action, IDisposable> _scheduleSave;
    private readonly Action<string>? _notice;
    private IDisposable? _pending;

    /// <param name="store">Loaded once, here; saved on every debounced change.</param>
    /// <param name="scheduleSave">Runs the action once, <see cref="SaveDelay"/> after the call, on the writer thread; disposing the result cancels it.</param>
    /// <param name="notice">Receives load fallbacks, rejected items and failed background saves.</param>
    public ConfigSession(IConfigStore store, Func<Action, IDisposable> scheduleSave, Action<string>? notice = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(scheduleSave);
        Store = store;
        _scheduleSave = scheduleSave;
        _notice = notice;

        var loaded = store.Load();
        Settings = new SettingsStore(ValidSettings(loaded.Settings));
        Gestures = ValidLibrary(loaded.Gestures);
        Mapping = ValidMapping(loaded.Mapping);
        Settings.Changed += OnChanged;
        Gestures.Changed += OnChanged;
        Mapping.Changed += OnChanged;
    }

    public IConfigStore Store { get; }

    public SettingsStore Settings { get; }

    public GestureLibrary Gestures { get; }

    public MappingStore Mapping { get; }

    /// <summary>The document as it would be written now, rebuilt from the stores.</summary>
    public ConfigDocument Document => new() { Settings = Settings.Current, Gestures = Gestures.All, Mapping = Mapping.Current };

    /// <summary>True between a change and the write that persists it.</summary>
    public bool HasPendingSave { get; private set; }

    /// <summary>Raised synchronously on every store change, before the save is scheduled; the engine swaps config here.</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>Writes now if anything is pending. Call before quitting.</summary>
    public void Flush()
    {
        CancelPending();
        if (HasPendingSave)
        {
            SaveNow();
        }
    }

    public void Dispose()
    {
        Settings.Changed -= OnChanged;
        Gestures.Changed -= OnChanged;
        Mapping.Changed -= OnChanged;
        Flush();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        HasPendingSave = true;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        CancelPending();
        _pending = _scheduleSave(SaveScheduled);
    }

    private void SaveScheduled()
    {
        _pending = null;
        try
        {
            SaveNow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _notice?.Invoke($"Could not save {Store.Location}: {ex.Message}");
        }
    }

    private void SaveNow()
    {
        Store.Save(Document);
        HasPendingSave = false;
    }

    private void CancelPending()
    {
        _pending?.Dispose();
        _pending = null;
    }

    private Settings ValidSettings(Settings settings)
    {
        try
        {
            SettingsRules.EnsureValid(settings);
            return settings;
        }
        catch (SettingsValidationException ex)
        {
            _notice?.Invoke($"Settings reset to defaults: {ex.Message}");
            return Config.Settings.Default;
        }
    }

    /// <summary>Strict first; if the saved set breaks a rule, keep every gesture that passes on its own and report the rest.</summary>
    private GestureLibrary ValidLibrary(IReadOnlyList<Gesture> gestures)
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
                    _notice?.Invoke($"Gesture '{gesture.Name}' ({gesture.Id}) skipped: {ex.Message}");
                }
            }

            library.ClearHistory();
            return library;
        }
    }

    /// <summary>Strict first; if the saved mapping breaks a rule, load group by group and command by command, keep what passes and report the rest.</summary>
    private MappingStore ValidMapping(MappingDocument mapping)
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
                    _notice?.Invoke($"App group '{group.Name}' ({group.Id}) skipped: only one Global group is allowed.");
                    continue;
                }

                globalLoaded |= group.IsGlobal;
                LoadGroup(store, group);
            }

            foreach (var app in mapping.Ignored)
            {
                Try(() => store.AddIgnored(app), $"Ignored app '{app.Name}' ({app.Id})");
            }

            store.ClearHistory();
            return store;
        }
    }

    /// <summary>
    /// The group without its commands, categories and hold remaps first, then each category, each hold remap, each command: a
    /// bad category costs only itself (its commands load Uncategorized), and so does a bad hold remap (its commands load as
    /// ordinary commands without their input).
    /// </summary>
    private void LoadGroup(MappingStore store, AppGroup group)
    {
        var shell = group with { Commands = [], Categories = [], HoldRemaps = [] };
        if (!Try(() => _ = group.IsGlobal ? store.UpdateGroup(shell) : store.AddGroup(shell), $"App group '{group.Name}' ({group.Id})"))
        {
            return;
        }

        foreach (var category in group.Categories)
        {
            Try(() => AddCategory(store, group.Id, category), $"Category '{category.Name}' ({category.Id}) in '{group.Name}'");
        }

        foreach (var holdRemap in group.HoldRemaps)
        {
            Try(() => store.AddHoldRemap(group.Id, holdRemap), $"Hold remap '{holdRemap.Name}' ({holdRemap.Id}) in '{group.Name}'");
        }

        foreach (var command in group.Commands)
        {
            Try(() => store.AddCommand(group.Id, command), $"Command '{command.Name}' ({command.Id}) in '{group.Name}'");
        }
    }

    private static void AddCategory(MappingStore store, GroupId groupId, CommandCategory category)
    {
        var group = store.FindGroup(groupId)!;
        store.UpdateGroup(group with { Categories = [.. group.Categories, category] });
    }

    private bool Try(Action load, string what)
    {
        try
        {
            load();
            return true;
        }
        catch (MappingValidationException ex)
        {
            _notice?.Invoke($"{what} skipped: {ex.Message}");
            return false;
        }
    }
}
