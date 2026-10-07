using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Inspector;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandsWorkbench;

/// <summary>
/// Lookless layout of the Commands tab (F5a, F7): the <see cref="CommandTree.CommandTree"/>
/// (<c>PART_Tree</c>) beside the selected command's <see cref="CommandHeader"/> (<c>PART_Header</c>)
/// and <see cref="StepList.StepList"/> (<c>PART_Steps</c>), with the message line. Where the step
/// panel sits is the template's choice (ADR-0002 §5b). It passes properties down and raises its
/// parts' intents up unchanged: <see cref="TreeActionRequested"/> for the tree and the header,
/// <see cref="StepActionRequested"/> for the step list.
/// </summary>
public sealed class CommandsWorkbench : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GroupItem>> GroupsProperty =
        AvaloniaProperty.Register<CommandsWorkbench, IReadOnlyList<GroupItem>>(nameof(Groups), []);

    public static readonly StyledProperty<GroupId?> SelectedGroupIdProperty =
        AvaloniaProperty.Register<CommandsWorkbench, GroupId?>(nameof(SelectedGroupId));

    public static readonly StyledProperty<CommandId?> SelectedCommandIdProperty =
        AvaloniaProperty.Register<CommandsWorkbench, CommandId?>(nameof(SelectedCommandId));

    public static readonly StyledProperty<CommandItem?> SelectedCommandProperty =
        AvaloniaProperty.Register<CommandsWorkbench, CommandItem?>(nameof(SelectedCommand));

    public static readonly StyledProperty<IReadOnlyList<StepItem>> StepsProperty =
        AvaloniaProperty.Register<CommandsWorkbench, IReadOnlyList<StepItem>>(nameof(Steps), []);

    public static readonly StyledProperty<int> SelectedStepIndexProperty =
        AvaloniaProperty.Register<CommandsWorkbench, int>(nameof(SelectedStepIndex), -1);

    public static readonly StyledProperty<IReadOnlyList<IStepType>> StepTypesProperty =
        AvaloniaProperty.Register<CommandsWorkbench, IReadOnlyList<IStepType>>(nameof(StepTypes), []);

    public static readonly StyledProperty<bool> CanUndoProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(CanUndo));

    public static readonly StyledProperty<bool> CanRedoProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(CanRedo));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<CommandsWorkbench, string?>(nameof(Message));

    public static readonly StyledProperty<bool> HasCommandProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(HasCommand));

    public event EventHandler<CommandTreeActionEventArgs>? TreeActionRequested;

    public event EventHandler<StepListActionEventArgs>? StepActionRequested;

    public IReadOnlyList<GroupItem> Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public GroupId? SelectedGroupId
    {
        get => GetValue(SelectedGroupIdProperty);
        set => SetValue(SelectedGroupIdProperty, value);
    }

    public CommandId? SelectedCommandId
    {
        get => GetValue(SelectedCommandIdProperty);
        set => SetValue(SelectedCommandIdProperty, value);
    }

    public CommandItem? SelectedCommand
    {
        get => GetValue(SelectedCommandProperty);
        set => SetValue(SelectedCommandProperty, value);
    }

    public IReadOnlyList<StepItem> Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public int SelectedStepIndex
    {
        get => GetValue(SelectedStepIndexProperty);
        set => SetValue(SelectedStepIndexProperty, value);
    }

    public IReadOnlyList<IStepType> StepTypes
    {
        get => GetValue(StepTypesProperty);
        set => SetValue(StepTypesProperty, value);
    }

    public bool CanUndo
    {
        get => GetValue(CanUndoProperty);
        set => SetValue(CanUndoProperty, value);
    }

    public bool CanRedo
    {
        get => GetValue(CanRedoProperty);
        set => SetValue(CanRedoProperty, value);
    }

    /// <summary>Rule messages and feedback ("Deleted 'X'. Ctrl+Z undoes it.").</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool HasCommand
    {
        get => GetValue(HasCommandProperty);
        private set => SetValue(HasCommandProperty, value);
    }

    public CommandTree.CommandTree? TreePart { get; private set; }

    public CommandHeader? HeaderPart { get; private set; }

    public StepList.StepList? StepsPart { get; private set; }

    /// <summary>Selects the command in the tree and starts renaming it in place (a fresh "New command N").</summary>
    public void BeginRename(CommandId id) => TreePart?.BeginRename(id);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TreePart = e.NameScope.Find<CommandTree.CommandTree>("PART_Tree");
        HeaderPart = e.NameScope.Find<CommandHeader>("PART_Header");
        StepsPart = e.NameScope.Find<StepList.StepList>("PART_Steps");
        if (TreePart is not null)
        {
            Region.Mark(TreePart, "Command tree");
            TreePart.ActionRequested += (_, args) => TreeActionRequested?.Invoke(this, args);
        }

        if (HeaderPart is not null)
        {
            Region.Mark(HeaderPart, "Command header");
            HeaderPart.ActionRequested += (_, args) => TreeActionRequested?.Invoke(this, args);
        }

        if (StepsPart is not null)
        {
            Region.Mark(StepsPart, "Step list");
            StepsPart.ActionRequested += (_, args) => StepActionRequested?.Invoke(this, args);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedCommandProperty)
        {
            HasCommand = SelectedCommand is not null;
        }
    }
}
