using Augram.Core.Gestures;

namespace Augram.Core.Config;

/// <summary>
/// Owns the store, the loaded document and the two aggregate stores for the app's lifetime,
/// and implements live save (checklist A3): any change to either store schedules one
/// debounced write of the whole document <see cref="SaveDelay"/> later; a burst of edits is
/// one file write and one backup. The scheduler is injected so the host runs the save on
/// its UI thread (same thread as the edits; this type is single-writer) and tests run it by hand.
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
        Settings.Changed += OnChanged;
        Gestures.Changed += OnChanged;
    }

    public IConfigStore Store { get; }

    public SettingsStore Settings { get; }

    public GestureLibrary Gestures { get; }

    /// <summary>The document as it would be written now, rebuilt from the stores.</summary>
    public ConfigDocument Document => new() { Settings = Settings.Current, Gestures = Gestures.All };

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
}
