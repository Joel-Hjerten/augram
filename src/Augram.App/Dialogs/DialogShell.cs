using Augram.App.Components.Parts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Dialogs;

/// <summary>
/// The dialog chrome, lookless (its ControlTheme is in <c>Themes/Default/Lists.axaml</c>): an optional <see cref="Heading"/> with
/// its ⓘ (<see cref="Help"/>) and, right-aligned on the same row, any <see cref="HeadingTrailing"/> (the gesture picker's search box,
/// as the Gestures tab places its own), the <see cref="ContentControl.Content"/> filling the middle, and the buttons bottom right in
/// <c>PART_Buttons</c>: any <see cref="LeadingButtons"/>, then <see cref="CancelLabel"/>, then <see cref="ConfirmLabel"/> (the default
/// button, enabled while <see cref="CanConfirm"/>). A null label leaves its button out (a message has OK only; "Used by…" has Close
/// only). The two buttons raise <see cref="Confirmed"/> and <see cref="Cancelled"/>; the host decides what they mean. Escape is the
/// window's (<see cref="DialogWindow"/>), never the shell's. Hosts: every dialog (<c>Dialogs/README.md</c> lists them).
/// </summary>
public class DialogShell : ContentControl
{
    public static readonly StyledProperty<string?> HeadingProperty =
        AvaloniaProperty.Register<DialogShell, string?>(nameof(Heading));

    public static readonly StyledProperty<string?> HelpProperty =
        AvaloniaProperty.Register<DialogShell, string?>(nameof(Help));

    public static readonly StyledProperty<string?> ConfirmLabelProperty =
        AvaloniaProperty.Register<DialogShell, string?>(nameof(ConfirmLabel), "OK");

    public static readonly StyledProperty<string?> CancelLabelProperty =
        AvaloniaProperty.Register<DialogShell, string?>(nameof(CancelLabel), "Cancel");

    public static readonly StyledProperty<bool> CanConfirmProperty =
        AvaloniaProperty.Register<DialogShell, bool>(nameof(CanConfirm), true);

    public static readonly StyledProperty<object?> HeadingTrailingProperty =
        AvaloniaProperty.Register<DialogShell, object?>(nameof(HeadingTrailing));

    public static readonly StyledProperty<bool> HasHeadingProperty =
        AvaloniaProperty.Register<DialogShell, bool>(nameof(HasHeading));

    public static readonly StyledProperty<bool> HasHelpProperty =
        AvaloniaProperty.Register<DialogShell, bool>(nameof(HasHelp));

    private Panel? _buttons;

    public DialogShell()
    {
        LeadingButtons.CollectionChanged += (_, _) => BuildButtons();
    }

    public event EventHandler? Confirmed;

    public event EventHandler? Cancelled;

    public string? Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>The heading's ⓘ text (App README, help rule); no ⓘ without it.</summary>
    public string? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    /// <summary>The default button's label; null leaves the button out.</summary>
    public string? ConfirmLabel
    {
        get => GetValue(ConfirmLabelProperty);
        set => SetValue(ConfirmLabelProperty, value);
    }

    /// <summary>The first button's label ("Cancel", "Close"); null leaves the button out.</summary>
    public string? CancelLabel
    {
        get => GetValue(CancelLabelProperty);
        set => SetValue(CancelLabelProperty, value);
    }

    public bool CanConfirm
    {
        get => GetValue(CanConfirmProperty);
        set => SetValue(CanConfirmProperty, value);
    }

    /// <summary>Content right-aligned on the heading row (a search box); null leaves the row to the heading.</summary>
    public object? HeadingTrailing
    {
        get => GetValue(HeadingTrailingProperty);
        set => SetValue(HeadingTrailingProperty, value);
    }

    public bool HasHeading
    {
        get => GetValue(HasHeadingProperty);
        private set => SetValue(HasHeadingProperty, value);
    }

    public bool HasHelp
    {
        get => GetValue(HasHelpProperty);
        private set => SetValue(HasHelpProperty, value);
    }

    /// <summary>
    /// Buttons placed before Cancel, for a dialog with more ways out than cancel and confirm (the gesture picker's No Gesture and
    /// New Gesture…). Each keeps its own click; the shell only places them.
    /// </summary>
    public Avalonia.Controls.Controls LeadingButtons { get; } = new();

    /// <summary>The confirm button once the template is applied and while there is one; tests press it.</summary>
    public Button? ConfirmButton { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _buttons = e.NameScope.Find<Panel>("PART_Buttons");
        BuildButtons();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeadingProperty)
        {
            HasHeading = !string.IsNullOrEmpty(Heading);
        }
        else if (change.Property == HelpProperty)
        {
            HasHelp = !string.IsNullOrEmpty(Help);
        }
        else if (change.Property == ConfirmLabelProperty || change.Property == CancelLabelProperty)
        {
            BuildButtons();
        }
        else if (change.Property == CanConfirmProperty && ConfirmButton is { } confirm)
        {
            confirm.IsEnabled = CanConfirm;
        }
    }

    private void BuildButtons()
    {
        if (_buttons is null)
        {
            return;
        }

        _buttons.Children.Clear();
        ConfirmButton = null;
        _buttons.Children.AddRange(LeadingButtons);
        if (CancelLabel is { } cancelLabel)
        {
            var cancel = ToolbarButton.Create(cancelLabel, () => Cancelled?.Invoke(this, EventArgs.Empty));
            cancel.IsCancel = true;
            _buttons.Children.Add(cancel);
        }

        if (ConfirmLabel is { } confirmLabel)
        {
            var confirm = ToolbarButton.Create(confirmLabel, () => Confirmed?.Invoke(this, EventArgs.Empty));
            confirm.IsDefault = true;
            confirm.IsEnabled = CanConfirm;
            _buttons.Children.Add(confirm);
            ConfirmButton = confirm;
        }
    }
}
