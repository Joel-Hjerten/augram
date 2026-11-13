using System.Globalization;
using Augram.App.Components.CommandTree;
using Augram.App.Components.Parts;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The drag distance row of <see cref="CommandHeader"/> (Joel, 2026-10-10, plan 0004), shown only while the trigger holds
/// buttons other than the stroke button and not the stroke button (<see cref="CommandHeaderItem.ShowsDragDistance"/>: Right in
/// Right + wheel), whose presses are held back and handed back as drags. <c>PART_DragDistanceMode</c> chooses between Options ›
/// Strokes › Capture's value ("Options value (10 px)", <see cref="CommandHeaderItem.OptionsDragDistancePx"/>) and the command's own, which
/// <c>PART_DragDistance</c> (1 to <see cref="TriggerHold.MaxDragDistancePx"/> px) then shows. Like the "While holding" boxes it
/// only asks (<see cref="CommandTreeAction.SetTriggerHold"/> with <see cref="TriggerHold.DragDistancePx"/> set or cleared), so
/// it goes through the host's one trigger edit and is one undo step; the row then shows the item's trigger again. For a drag
/// trigger (plan 0015) the same row is labelled "Then drag" and sits right under While holding, where a wheel or button
/// trigger has its "Then wheel" / "Then press" row: the distance is what fires it.
/// </summary>
public sealed partial class CommandHeader
{
    /// <summary>The ⓘ beside "Drag distance".</summary>
    public const string DragDistanceHelp =
        "How far a press of the held button may move before Augram gives it to the app as a drag. Lower starts drags sooner; too low and a wobble while turning the wheel gives the press away. Commands sharing a button over one app use the largest.";

    /// <summary>The ⓘ beside "Then drag" on a drag trigger (plan 0015).</summary>
    public const string ThenDragHelp =
        "How far the held button must move before the drag fires. A click without moving, or a shorter move, still reaches the app. Commands sharing a button over one app use the largest.";

    /// <summary>The second choice of <c>PART_DragDistanceMode</c>: the command's own distance, in the box beside it.</summary>
    public const string OwnDragDistanceLabel = "Own distance";

    /// <summary>The row's label for a wheel or button trigger.</summary>
    public const string DragDistanceLabelText = "Drag distance";

    public static readonly StyledProperty<bool> ShowsDragDistanceProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(ShowsDragDistance));

    public static readonly StyledProperty<bool> ShowsDragDistanceRowProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(ShowsDragDistanceRow));

    public static readonly StyledProperty<string> DragDistanceLabelProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(DragDistanceLabel), DragDistanceLabelText);

    public static readonly StyledProperty<string> DragDistanceTipProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(DragDistanceTip), DragDistanceHelp);

    public static readonly StyledProperty<bool> HasOwnDragDistanceProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasOwnDragDistance));

    private AskingDropdown? _dragMode;
    private NumericUpDown? _dragDistance;

    /// <summary>Shows the Drag distance row: the trigger hands back drags.</summary>
    public bool ShowsDragDistance
    {
        get => GetValue(ShowsDragDistanceProperty);
        private set => SetValue(ShowsDragDistanceProperty, value);
    }

    /// <summary>
    /// Shows the row: the trigger hands back drags, or it is a drag trigger (plan 0015), whose row reads "Then drag" even before
    /// its button is ticked (its choices then wait for the button).
    /// </summary>
    public bool ShowsDragDistanceRow
    {
        get => GetValue(ShowsDragDistanceRowProperty);
        private set => SetValue(ShowsDragDistanceRowProperty, value);
    }

    /// <summary>"Drag distance", or "Then drag" for a drag trigger (Joel, 2026-11-13: the card reads in the order things happen).</summary>
    public string DragDistanceLabel
    {
        get => GetValue(DragDistanceLabelProperty);
        private set => SetValue(DragDistanceLabelProperty, value);
    }

    /// <summary>The row's ⓘ: <see cref="DragDistanceHelp"/>, or <see cref="ThenDragHelp"/> for a drag trigger.</summary>
    public string DragDistanceTip
    {
        get => GetValue(DragDistanceTipProperty);
        private set => SetValue(DragDistanceTipProperty, value);
    }

    /// <summary>Shows the number box: the trigger has its own distance rather than the Options value.</summary>
    public bool HasOwnDragDistance
    {
        get => GetValue(HasOwnDragDistanceProperty);
        private set => SetValue(HasOwnDragDistanceProperty, value);
    }

    /// <summary>The choice the mode dropdown shows: 0 the Options value, 1 its own; -1 while the row is hidden.</summary>
    public int DragDistanceModeIndex => _dragMode?.SelectedIndex ?? -1;

    /// <summary>"Options value (10 px)": the mode dropdown's first choice.</summary>
    public static string OptionsDragDistanceLabel(int optionsPx)
        => string.Create(CultureInfo.InvariantCulture, $"Options value ({optionsPx} px)");

    /// <summary>
    /// What the row does: asks the host for the trigger's set with <paramref name="distancePx"/> as its own drag distance (null:
    /// the Options value). A value outside 1 to <see cref="TriggerHold.MaxDragDistancePx"/> is brought inside first.
    /// </summary>
    public void ChooseDragDistance(int? distancePx)
    {
        if (Item is { ShowsDragDistance: true } item)
        {
            var wanted = distancePx is { } px ? Math.Clamp(px, 1, TriggerHold.MaxDragDistancePx) : (int?)null;
            if (wanted != item.Trigger.Hold.DragDistancePx)
            {
                ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerHold, command: item, hold: item.Trigger.Hold with { DragDistancePx = wanted }));
            }
        }

        ApplyDragDistance();
    }

    private void FindDragDistanceParts(TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<ComboBox>("PART_DragDistanceMode") is { } mode)
        {
            // Own starts at the distance the trigger had, the Options value, so choosing it changes nothing yet but the box.
            _dragMode = new AskingDropdown(mode, index =>
            {
                if (Item is { } item)
                {
                    ChooseDragDistance(index == 0 ? null : item.Trigger.Hold.DragDistancePx ?? item.OptionsDragDistancePx);
                }
            });
        }

        _dragDistance = e.NameScope.Find<NumericUpDown>("PART_DragDistance");
        if (_dragDistance is not null)
        {
            _dragDistance.Minimum = 1;
            _dragDistance.Maximum = TriggerHold.MaxDragDistancePx;
            _dragDistance.Increment = 1;
            _dragDistance.FormatString = "0";
            _dragDistance.ValueChanged += (_, changed) =>
            {
                if (!_applying && changed.NewValue is { } value && Item is { Trigger.Hold.DragDistancePx: not null })
                {
                    ChooseDragDistance((int)Math.Round(value));
                }
            };
        }
    }

    /// <summary>Puts the row on the item's trigger: shown or not, the Options value or its own, and the number.</summary>
    private void ApplyDragDistance()
    {
        var item = Item;
        var shows = item is { ShowsDragDistance: true };
        var own = shows ? item!.Trigger.Hold.DragDistancePx : null;
        var drag = item is { HoldRemap: null, Trigger: Trigger.DragTrigger };
        ShowsDragDistance = shows;
        ShowsDragDistanceRow = shows || drag;
        DragDistanceLabel = drag ? TriggerKind.Drag.ThenLabel()! : DragDistanceLabelText;
        DragDistanceTip = drag ? ThenDragHelp : DragDistanceHelp;
        HasOwnDragDistance = own is not null;
        _dragMode?.Show(shows ? [OptionsDragDistanceLabel(item!.OptionsDragDistancePx), OwnDragDistanceLabel] : [], shows ? (own is null ? 0 : 1) : -1);
        if (_dragDistance is not null && own is { } px)
        {
            _applying = true;
            try
            {
                _dragDistance.Value = px;
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
