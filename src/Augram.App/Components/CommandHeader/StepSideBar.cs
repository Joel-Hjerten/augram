using Augram.App.Components.CommandTree;
using Augram.App.Components.Parts;
using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// Lookless "Steps for" row (plan 0010), drawn as the first row of the Steps card (Joel, 0.14.3: as "Fields for" is of the App
/// identification card, so it reads as tied to the steps): a Windows · macOS switch (<c>PART_StepsForWindows</c>,
/// <c>PART_StepsForMac</c>, the "Fields for" switch's segmented look, this computer's segment outlined with <c>here</c>) that asks
/// the host to show that platform's steps, Copy from the other platform (<c>PART_CopySteps</c>), Mark as checked
/// (<c>PART_MarkChecked</c>) and a note: on the other platform's side "Edit on Windows", on this one the row's F8 sentence ("From
/// Windows · 1 needs a macOS version"; Joel, 0.19.7: moved here from the command row, which keeps a badge). All of it comes from
/// <see cref="Item"/>'s <see cref="CommandHeaderItem.StepSide"/>; it only asks (<see cref="ActionRequested"/>, the tree's actions)
/// and then shows its item again.
/// </summary>
public sealed class StepSideBar : TemplatedControl
{
    /// <summary>The Steps for row's ⓘ.</summary>
    public const string StepsForHelp = "This computer runs its platform's steps, and only those are edited here. The other platform's show read-only: they are its own, or converted from these until it has its own. Copy from replaces the steps in view with the other platform's.";

    public static readonly StyledProperty<CommandHeaderItem?> ItemProperty =
        AvaloniaProperty.Register<StepSideBar, CommandHeaderItem?>(nameof(Item));

    public static readonly StyledProperty<bool> HasStepSideProperty =
        AvaloniaProperty.Register<StepSideBar, bool>(nameof(HasStepSide));

    public static readonly StyledProperty<string?> StepSideTextProperty =
        AvaloniaProperty.Register<StepSideBar, string?>(nameof(StepSideText));

    public static readonly StyledProperty<string?> CopyStepsTextProperty =
        AvaloniaProperty.Register<StepSideBar, string?>(nameof(CopyStepsText));

    public static readonly StyledProperty<bool> CanMarkCheckedProperty =
        AvaloniaProperty.Register<StepSideBar, bool>(nameof(CanMarkChecked));

    private RadioButton? _stepsForWindows;
    private RadioButton? _stepsForMac;
    private bool _applying;

    public event EventHandler<CommandTreeActionEventArgs>? ActionRequested;

    /// <summary>The selected command; its <see cref="CommandHeaderItem.StepSide"/> is what the row shows, and it is the command each request names.</summary>
    public CommandHeaderItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    /// <summary>A command is selected: the row shows.</summary>
    public bool HasStepSide
    {
        get => GetValue(HasStepSideProperty);
        private set => SetValue(HasStepSideProperty, value);
    }

    /// <summary>On the other platform's side, its note: "Edit on Windows"; on this one where the steps come from ("From Windows · converted"), null for steps authored here.</summary>
    public string? StepSideText
    {
        get => GetValue(StepSideTextProperty);
        private set => SetValue(StepSideTextProperty, value);
    }

    /// <summary>"Copy from Windows" when both sides have their own steps; null hides the button.</summary>
    public string? CopyStepsText
    {
        get => GetValue(CopyStepsTextProperty);
        private set => SetValue(CopyStepsTextProperty, value);
    }

    /// <summary>F8: the own steps here were made before the original last changed; shows "Mark as checked".</summary>
    public bool CanMarkChecked
    {
        get => GetValue(CanMarkCheckedProperty);
        private set => SetValue(CanMarkCheckedProperty, value);
    }

    /// <summary>What the switch does: asks the host to show <paramref name="platform"/>'s steps; the switch then shows the answer.</summary>
    public void ShowStepsFor(HostPlatform platform)
    {
        if (Item is { StepSide: { } side } item && side.Shown != platform)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.ShowStepsFor, command: item, platform: platform));
        }

        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stepsForWindows = e.NameScope.Find<RadioButton>("PART_StepsForWindows");
        _stepsForMac = e.NameScope.Find<RadioButton>("PART_StepsForMac");
        Wire(_stepsForWindows, HostPlatform.Windows);
        Wire(_stepsForMac, HostPlatform.MacOS);
        WireAction(e, "PART_CopySteps", CommandTreeAction.CopyStepsFromOtherPlatform);
        WireAction(e, "PART_MarkChecked", CommandTreeAction.MarkOwnVersionChecked);
        Apply();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var side = Item?.StepSide;
            HasStepSide = side is not null;
            StepSideText = side is { IsHere: true } ? Sentence(Item?.PlatformMarker) : side?.EditNote;
            CopyStepsText = side?.CopyText;
            CanMarkChecked = side is { CanMarkChecked: true };
            Apply();
        }
    }

    private static string? Sentence(string? marker) => string.IsNullOrEmpty(marker) ? null : char.ToUpperInvariant(marker[0]) + marker[1..];

    private void Apply()
    {
        _applying = true;
        try
        {
            var side = Item?.StepSide;
            var shown = side?.Shown;
            // This computer's platform is outlined whichever side is in view (Joel, 0.14.1), as on the "Fields for" switch.
            var here = side is null ? (HostPlatform?)null : side.IsHere ? side.Shown : side.Shown.Other();
            if (_stepsForWindows is not null)
            {
                _stepsForWindows.IsChecked = shown == HostPlatform.Windows;
                _stepsForWindows.Classes.Set("here", here == HostPlatform.Windows);
            }

            if (_stepsForMac is not null)
            {
                _stepsForMac.IsChecked = shown == HostPlatform.MacOS;
                _stepsForMac.Classes.Set("here", here == HostPlatform.MacOS);
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private void Wire(RadioButton? button, HostPlatform platform)
    {
        if (button is not null)
        {
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_applying && button.IsChecked == true)
                {
                    ShowStepsFor(platform);
                }
            };
        }
    }

    private void WireAction(TemplateAppliedEventArgs e, string part, CommandTreeAction action) => e.OnClick(part, () =>
    {
        if (Item is { } item)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, command: item));
        }
    });
}
