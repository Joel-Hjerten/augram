using Augram.App.Components.CommandTree;
using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The "Steps for" row of <see cref="CommandHeader"/> (plan 0010): a Windows · macOS switch (<c>PART_StepsForWindows</c>,
/// <c>PART_StepsForMac</c>, the "Fields for" switch's segmented look) that asks the host to show that platform's steps, the
/// side's label ("macOS · runs here"), and under it the other side's note ("Edit on Windows"), Copy from the other platform
/// (<c>PART_CopySteps</c>) and Mark as checked (<c>PART_MarkChecked</c>). All of it comes from <see cref="CommandHeaderItem.StepSide"/>.
/// </summary>
public sealed partial class CommandHeader
{
    /// <summary>The Steps for row's ⓘ.</summary>
    public const string StepsForHelp = "This computer runs its platform's steps, and only those are edited here. The other platform's show read-only: they are its own, or converted from these until it has its own. Copy from replaces the steps in view with the other platform's.";

    public static readonly StyledProperty<bool> HasStepSideProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasStepSide));

    public static readonly StyledProperty<string?> StepSideTextProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(StepSideText));

    public static readonly StyledProperty<string?> CopyStepsTextProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(CopyStepsText));

    public static readonly StyledProperty<bool> CanMarkCheckedProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanMarkChecked));


    private RadioButton? _stepsForWindows;
    private RadioButton? _stepsForMac;

    /// <summary>A command is selected: the Steps for row shows.</summary>
    public bool HasStepSide
    {
        get => GetValue(HasStepSideProperty);
        private set => SetValue(HasStepSideProperty, value);
    }

    /// <summary>The side in view: "macOS · runs here", "Windows · converted from macOS".</summary>
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

        ApplyStepSide();
    }

    private void ShowStepSide(StepSide? side)
    {
        HasStepSide = side is not null;
        StepSideText = side?.EditNote;
        CopyStepsText = side?.CopyText;
        CanMarkChecked = side is { CanMarkChecked: true };
    }

    private void FindStepSideParts(TemplateAppliedEventArgs e)
    {
        _stepsForWindows = e.NameScope.Find<RadioButton>("PART_StepsForWindows");
        _stepsForMac = e.NameScope.Find<RadioButton>("PART_StepsForMac");
        WireStepSide(_stepsForWindows, HostPlatform.Windows);
        WireStepSide(_stepsForMac, HostPlatform.MacOS);
    }

    private void ApplyStepSide()
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

    private void WireStepSide(RadioButton? button, HostPlatform platform)
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
}
