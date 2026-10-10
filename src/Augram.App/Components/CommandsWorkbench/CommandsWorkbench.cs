using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Components.Steps;
using Augram.App.Declarations;
using Augram.App.Inspector;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandsWorkbench;

/// <summary>
/// Lookless layout of a Commands sub-tab (F5a, F7; Global and Apps since 2026-10-07): the
/// <see cref="CommandTree.CommandTree"/> (<c>PART_Tree</c>) beside the selected command's
/// <see cref="CommandHeader"/> (<c>PART_Header</c>) and <see cref="StepList.StepList"/> (<c>PART_Steps</c>),
/// or, while an app group, hold remap or category row is selected, its form (<see cref="GroupForm"/>, <c>PART_GroupForm</c>), with
/// the message line. Both sub-tabs use it; what differs (the sections, the tree's heading, the
/// new-section label, the help line) comes in as properties. Where the step panel sits is the template's
/// choice (ADR-0002 §5b). It passes properties down and raises its parts' intents up unchanged:
/// <see cref="TreeActionRequested"/> for the tree and the header, <see cref="StepActionRequested"/> for
/// the step list.
/// </summary>
public sealed class CommandsWorkbench : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<SectionItem>> SectionsProperty =
        AvaloniaProperty.Register<CommandsWorkbench, IReadOnlyList<SectionItem>>(nameof(Sections), []);

    public static readonly StyledProperty<SectionId?> SelectedSectionIdProperty =
        AvaloniaProperty.Register<CommandsWorkbench, SectionId?>(nameof(SelectedSectionId));

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

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<CommandsWorkbench, string?>(nameof(Message));

    public static readonly StyledProperty<bool> HasCommandProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(HasCommand));

    public static readonly StyledProperty<bool> IsUnderHoldRemapProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(IsUnderHoldRemap));

    public static readonly StyledProperty<bool> OffersRemapStepProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(OffersRemapStep));

    public static readonly StyledProperty<StepFormContext> StepFormContextProperty =
        AvaloniaProperty.Register<CommandsWorkbench, StepFormContext>(nameof(StepFormContext), StepFormContext.None);

    public static readonly StyledProperty<FormScreen?> GroupFormProperty =
        AvaloniaProperty.Register<CommandsWorkbench, FormScreen?>(nameof(GroupForm));

    public static readonly StyledProperty<bool> HasGroupFormProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(HasGroupForm));

    public static readonly StyledProperty<bool> ShowsCommandPanelProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(ShowsCommandPanel), true);

    public static readonly StyledProperty<string> TreeHeadingProperty =
        AvaloniaProperty.Register<CommandsWorkbench, string>(nameof(TreeHeading), "Commands");

    public static readonly StyledProperty<string> NewSectionLabelProperty =
        AvaloniaProperty.Register<CommandsWorkbench, string>(nameof(NewSectionLabel), "New group");

    public static readonly StyledProperty<string?> PlatformFilterLabelProperty =
        AvaloniaProperty.Register<CommandsWorkbench, string?>(nameof(PlatformFilterLabel));

    public static readonly StyledProperty<bool> ShowsOtherPlatformsProperty =
        AvaloniaProperty.Register<CommandsWorkbench, bool>(nameof(ShowsOtherPlatforms));

    public static readonly StyledProperty<string> TreeHelpProperty =
        AvaloniaProperty.Register<CommandsWorkbench, string>(nameof(TreeHelp), string.Empty);

    public event EventHandler<CommandTreeActionEventArgs>? TreeActionRequested;

    public event EventHandler<StepListActionEventArgs>? StepActionRequested;

    public IReadOnlyList<SectionItem> Sections
    {
        get => GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    public SectionId? SelectedSectionId
    {
        get => GetValue(SelectedSectionIdProperty);
        set => SetValue(SelectedSectionIdProperty, value);
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

    /// <summary>The selected command is under a hold remap (plan 0002).</summary>
    public bool IsUnderHoldRemap
    {
        get => GetValue(IsUnderHoldRemapProperty);
        private set => SetValue(IsUnderHoldRemapProperty, value);
    }

    /// <summary>
    /// The selected command's step list offers the Remap step (<see cref="CommandItem.OffersRemapStep"/>): it is under a hold
    /// remap (plan 0002) or has a button trigger here (plan 0005).
    /// </summary>
    public bool OffersRemapStep
    {
        get => GetValue(OffersRemapStepProperty);
        private set => SetValue(OffersRemapStepProperty, value);
    }

    /// <summary>The selected command as its step forms see it (<see cref="StepList.StepList.FormContext"/>: its trigger here).</summary>
    public StepFormContext StepFormContext
    {
        get => GetValue(StepFormContextProperty);
        private set => SetValue(StepFormContextProperty, value);
    }

    /// <summary>The selected app group's or category's form; while set it takes the side panel's place of the command header and steps.</summary>
    public FormScreen? GroupForm
    {
        get => GetValue(GroupFormProperty);
        set => SetValue(GroupFormProperty, value);
    }

    public bool HasGroupForm
    {
        get => GetValue(HasGroupFormProperty);
        private set => SetValue(HasGroupFormProperty, value);
    }

    /// <summary>The command header and steps show unless an app group's or category's form does.</summary>
    public bool ShowsCommandPanel
    {
        get => GetValue(ShowsCommandPanelProperty);
        private set => SetValue(ShowsCommandPanelProperty, value);
    }

    /// <summary>The tree's title (<see cref="CommandTree.CommandTree.Heading"/>).</summary>
    public string TreeHeading
    {
        get => GetValue(TreeHeadingProperty);
        set => SetValue(TreeHeadingProperty, value);
    }

    /// <summary>The tree's new-section label (<see cref="CommandTree.CommandTree.NewSectionLabel"/>).</summary>
    public string NewSectionLabel
    {
        get => GetValue(NewSectionLabelProperty);
        set => SetValue(NewSectionLabelProperty, value);
    }

    /// <summary>The tree's platform filter label (<see cref="CommandTree.CommandTree.PlatformFilterLabel"/>); null on the Global tab.</summary>
    public string? PlatformFilterLabel
    {
        get => GetValue(PlatformFilterLabelProperty);
        set => SetValue(PlatformFilterLabelProperty, value);
    }

    /// <summary>Groups used only on the other platform are listed (<see cref="CommandTree.CommandTree.ShowsOtherPlatforms"/>).</summary>
    public bool ShowsOtherPlatforms
    {
        get => GetValue(ShowsOtherPlatformsProperty);
        set => SetValue(ShowsOtherPlatformsProperty, value);
    }

    /// <summary>The tree's help line (<see cref="CommandTree.CommandTree.HelpText"/>).</summary>
    public string TreeHelp
    {
        get => GetValue(TreeHelpProperty);
        set => SetValue(TreeHelpProperty, value);
    }

    public CommandTree.CommandTree? TreePart { get; private set; }

    public CommandHeader? HeaderPart { get; private set; }

    public StepList.StepList? StepsPart { get; private set; }

    public SectionForm.SectionForm? GroupFormPart { get; private set; }

    /// <summary>Selects the command in the tree and starts renaming it in place (a fresh "New command N").</summary>
    public void BeginRename(CommandId id) => TreePart?.BeginRename(id);

    /// <summary>Selects the section in the tree and starts renaming it in place (a fresh "New category N").</summary>
    public void BeginRename(SectionId id) => TreePart?.BeginRename(id);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TreePart = e.NameScope.Find<CommandTree.CommandTree>("PART_Tree");
        HeaderPart = e.NameScope.Find<CommandHeader>("PART_Header");
        StepsPart = e.NameScope.Find<StepList.StepList>("PART_Steps");
        GroupFormPart = e.NameScope.Find<SectionForm.SectionForm>("PART_GroupForm");
        if (GroupFormPart is not null)
        {
            Region.Mark(GroupFormPart, "App group, hold remap or category form");
        }

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
            IsUnderHoldRemap = SelectedCommand is { IsUnderHoldRemap: true };
            OffersRemapStep = SelectedCommand is { OffersRemapStep: true };

            // A new context rebuilds the expanded step form; an equal one (the same trigger) must not.
            var context = StepFormContext.For(SelectedCommand?.Trigger);
            if (context != StepFormContext)
            {
                StepFormContext = context;
            }
        }
        else if (change.Property == GroupFormProperty)
        {
            HasGroupForm = GroupForm is not null;
            ShowsCommandPanel = GroupForm is null;
        }
    }
}
