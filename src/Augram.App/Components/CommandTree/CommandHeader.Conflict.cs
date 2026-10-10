using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The draft note's buttons on <see cref="CommandHeader"/> (Joel, 2026-10-10: "a button in the red warning area where it gives
/// you the option to accept changes and go to the conflict and resolve it over there"). When the draft (a trigger, or an
/// input) is refused only because another command of the group uses it here (<see cref="CommandItem.ConflictName"/>), the
/// note's box holds <c>PART_TakeTrigger</c> ("Take it from 'Zoom Out'": the other is left with none and opened) and beside it
/// <c>PART_SwapTrigger</c> ("Swap with 'Zoom Out'": the other gets this command's previous one), each shown only when the host
/// found the rules accept it (<see cref="CommandItem.CanTakeTrigger"/>, <see cref="CommandItem.CanSwapTrigger"/>), each with a
/// tooltip saying what happens to the other command. Both only ask (<see cref="CommandTreeAction.TakeTrigger"/>,
/// <see cref="CommandTreeAction.SwapTrigger"/>).
/// </summary>
public sealed partial class CommandHeader
{
    public static readonly StyledProperty<bool> CanSwapTriggerProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanSwapTrigger));

    public static readonly StyledProperty<bool> CanTakeTriggerProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(CanTakeTrigger));

    public static readonly StyledProperty<bool> HasConflictActionsProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasConflictActions));

    public static readonly StyledProperty<string> SwapTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(SwapText), string.Empty);

    public static readonly StyledProperty<string> TakeTextProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(TakeText), string.Empty);

    public static readonly StyledProperty<string?> SwapTipProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(SwapTip));

    public static readonly StyledProperty<string?> TakeTipProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(TakeTip));

    /// <summary>Shows Swap in the draft's note: the rules accept the exchange with <see cref="CommandItem.ConflictName"/>.</summary>
    public bool CanSwapTrigger
    {
        get => GetValue(CanSwapTriggerProperty);
        private set => SetValue(CanSwapTriggerProperty, value);
    }

    /// <summary>Shows Take it in the draft's note: the rules accept this command taking it and the other left with none.</summary>
    public bool CanTakeTrigger
    {
        get => GetValue(CanTakeTriggerProperty);
        private set => SetValue(CanTakeTriggerProperty, value);
    }

    /// <summary>The note's row of buttons shows: it offers Take it, Swap or both.</summary>
    public bool HasConflictActions
    {
        get => GetValue(HasConflictActionsProperty);
        private set => SetValue(HasConflictActionsProperty, value);
    }

    /// <summary>"Swap with 'Zoom Out'".</summary>
    public string SwapText
    {
        get => GetValue(SwapTextProperty);
        private set => SetValue(SwapTextProperty, value);
    }

    /// <summary>"Take it from 'Zoom Out'".</summary>
    public string TakeText
    {
        get => GetValue(TakeTextProperty);
        private set => SetValue(TakeTextProperty, value);
    }

    /// <summary>"Swap: Zoom Out gets this command's previous trigger." ("input" for a command under a hold remap).</summary>
    public string? SwapTip
    {
        get => GetValue(SwapTipProperty);
        private set => SetValue(SwapTipProperty, value);
    }

    /// <summary>"Take it: Zoom Out is left with no trigger, and is opened so you can give it one."</summary>
    public string? TakeTip
    {
        get => GetValue(TakeTipProperty);
        private set => SetValue(TakeTipProperty, value);
    }

    /// <summary>What Swap does: asks the host to exchange the draft with the other command's.</summary>
    public void SwapTrigger() => AskConflict(CommandTreeAction.SwapTrigger, CanSwapTrigger);

    /// <summary>What Take it does: asks the host to give this command the draft and leave the other with none.</summary>
    public void TakeTrigger() => AskConflict(CommandTreeAction.TakeTrigger, CanTakeTrigger);

    private void AskConflict(CommandTreeAction action, bool offered)
    {
        if (offered && Item is { } item)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(action, command: item));
        }
    }

    private void FindConflictParts(TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<Button>("PART_SwapTrigger") is { } swap)
        {
            swap.Click += (_, _) => SwapTrigger();
        }

        if (e.NameScope.Find<Button>("PART_TakeTrigger") is { } take)
        {
            take.Click += (_, _) => TakeTrigger();
        }
    }

    /// <summary>Puts the note's buttons on the item: shown only for what it offers, named after the command that uses the draft here.</summary>
    private void ShowConflict(CommandItem? item)
    {
        var name = item?.ConflictName;
        CanSwapTrigger = item is { ConflictName: not null, CanSwapTrigger: true };
        CanTakeTrigger = item is { ConflictName: not null, CanTakeTrigger: true };
        HasConflictActions = CanSwapTrigger || CanTakeTrigger;
        var what = item is { IsUnderHoldRemap: true } ? "input" : "trigger";
        SwapText = name is null ? string.Empty : $"Swap with '{name}'";
        TakeText = name is null ? string.Empty : $"Take it from '{name}'";
        SwapTip = name is null ? null : $"Swap: {name} gets this command's previous {what}.";
        TakeTip = name is null ? null : $"Take it: {name} is left with no {what}, and is opened so you can give it one.";
    }
}
