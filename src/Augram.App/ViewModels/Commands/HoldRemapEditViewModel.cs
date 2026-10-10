using Augram.App.Components.HotkeyCapture;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The hold remap form's edit state (F9, plan 0002 step 4): name, hold key, tap time, active and where it is used ("Use on").
/// <see cref="Declare"/> is the form as a <see cref="FormScreen"/> (ADR-0002 §5c), shown in the Apps tab's side panel while a
/// hold remap row is selected, as a category's or a group's form is; <see cref="Apply"/> turns it back into the hold remap,
/// its id kept; <see cref="SyncFrom"/> re-reads a stored one after an undo or a rename in the tree. The hold key is the capture
/// field in one-key mode, which refuses Ctrl, Alt, Shift and Win with Core's reason (<see cref="HoldRemapRules.HoldKeyProblem(KeyCode)"/>).
/// While the name is still the one it was given (the old key's name, or "Hold remap" before a key was chosen), it follows the
/// hold key ("Space"). Nothing is validated here: the store's rules answer when the panel applies.
/// </summary>
public sealed partial class HoldRemapEditViewModel : ObservableObject
{
    /// <summary>The form's heading (Joel, 2026-10-10: the name and the words for it).</summary>
    public const string Title = "Hold remaps — remap inputs or run commands while a key is held";

    /// <summary>The end of the form's ⓘ (plan 0005 decision 7: a hold remap never uses the stroke button, so an exclusion leaves it alone).</summary>
    public const string WorksOverExclusions = "Works even where the app is on Exclusions › Global.";

    private bool _syncing;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial KeyCode HoldKey { get; set; }

    [ObservableProperty]
    public partial int TapTimeMs { get; set; } = HoldRemap.DefaultTapTimeMs;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial bool UseOnWindows { get; set; } = true;

    [ObservableProperty]
    public partial bool UseOnMac { get; set; } = true;

    public static HoldRemapEditViewModel From(HoldRemap holdRemap)
    {
        var edit = new HoldRemapEditViewModel();
        edit.SyncFrom(holdRemap);
        return edit;
    }

    /// <summary>Takes the stored hold remap's values (the name does not follow the key meanwhile); a property that already holds the value raises nothing.</summary>
    public void SyncFrom(HoldRemap holdRemap)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        _syncing = true;
        try
        {
            Name = holdRemap.Name;
            HoldKey = holdRemap.HoldKey;
            TapTimeMs = holdRemap.TapTimeMs;
            IsActive = holdRemap.IsActive;
            UseOnWindows = holdRemap.UseOn.Includes(HostPlatform.Windows);
            UseOnMac = holdRemap.UseOn.Includes(HostPlatform.MacOS);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>The existing hold remap with these settings.</summary>
    public HoldRemap Apply(HoldRemap existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with
        {
            Name = Name,
            HoldKey = HoldKey,
            TapTimeMs = TapTimeMs,
            IsActive = IsActive,
            UseOn = PlatformSet.None.With(HostPlatform.Windows, UseOnWindows).With(HostPlatform.MacOS, UseOnMac),
        };
    }

    public FormScreen Declare() => new("Hold remap",
    [
        new Section(Title,
        [
            new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown in the list inside its app group; the hold key's name until you rename it."),
            new CustomField("Hold key", HoldKeyField, Help: $"The key you hold. Any key but {HotkeyCaptureBox.ModifierWords()} or Fn: those are held for triggers. Unique in the app group."),
            new NumberField(
                "Tap time (ms)",
                new DelegateBinding<double>(() => TapTimeMs, value => TapTimeMs = (int)Math.Round(value), this),
                HoldRemapRules.MinTapTimeMs,
                HoldRemapRules.MaxTapTimeMs,
                Step: 10,
                Help: $"Released within this time with nothing used, the hold key types as usual (a tap). Longer, or with a mouse button or wheel turn in it, it types nothing. 0 never types it. Default {HoldRemap.DefaultTapTimeMs}."),
            new ToggleField("Active", new DelegateBinding<bool>(() => IsActive, value => IsActive = value, this), "An inactive hold remap leaves its key alone."),
            new TogglesField(
                "Use on",
                [
                    new ToggleOption("Windows", new DelegateBinding<bool>(() => UseOnWindows, value => UseOnWindows = value, this)),
                    new ToggleOption("macOS", new DelegateBinding<bool>(() => UseOnMac, value => UseOnMac = value, this)),
                ],
                "Applies to every command under it: a platform left unticked never fires them and hides the hold remap unless Show other platforms is on."),
        ],
        "While the hold key is held in this app, the commands under it fire on their inputs: a mouse button, buttons held together, a wheel direction or a key. A Remap step holds an output for as long as the input is held; other steps run once per press. Right-click the hold remap to add commands. "
            + WorksOverExclusions),
    ]);

    /// <summary>While the name is still the one it was given, it follows a new hold key ("Hold remap" → "Space", "Space" → "S").</summary>
    partial void OnHoldKeyChanged(KeyCode oldValue, KeyCode newValue)
    {
        if (_syncing)
        {
            return;
        }

        var given = MappingRules.NameComparer.Equals(Name, HoldRemap.DefaultName(oldValue))
            || (oldValue == KeyCode.None && FreeNames.IsNumbered(HoldRemap.DefaultName(KeyCode.None), Name));
        if (given)
        {
            Name = HoldRemap.DefaultName(newValue);
        }
    }

    /// <summary>The capture field in one-key mode; a key it takes becomes <see cref="HoldKey"/>, and it shows the key the form holds.</summary>
    private HotkeyCaptureBox HoldKeyField()
    {
        var box = new HotkeyCaptureBox { SingleKey = true, KeyProblem = HoldRemapRules.HoldKeyProblem, Key = HoldKey };
        box.Committed += (_, e) => HoldKey = e.Key;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HoldKey))
            {
                box.Key = HoldKey;
            }
        };
        return box;
    }
}
