using Augram.App.Declarations;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.FormDialog;

/// <summary>
/// Lookless dialog content shared by every small popup form (F7: dialog chrome implemented once):
/// an optional <see cref="Message"/> line, an optional declared <see cref="Screen"/> rendered by
/// <c>SectionForm</c>, and the buttons <c>PART_Confirm</c> (labelled <see cref="ConfirmLabel"/>) and
/// <c>PART_Cancel</c>, which raise <see cref="Confirmed"/> and <see cref="Cancelled"/>. The host
/// window closes on either.
/// </summary>
public sealed class FormDialog : TemplatedControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<FormDialog, string?>(nameof(Message));

    public static readonly StyledProperty<FormScreen?> ScreenProperty =
        AvaloniaProperty.Register<FormDialog, FormScreen?>(nameof(Screen));

    public static readonly StyledProperty<string> ConfirmLabelProperty =
        AvaloniaProperty.Register<FormDialog, string>(nameof(ConfirmLabel), "OK");

    public static readonly StyledProperty<bool> HasMessageProperty =
        AvaloniaProperty.Register<FormDialog, bool>(nameof(HasMessage));

    public static readonly StyledProperty<bool> HasScreenProperty =
        AvaloniaProperty.Register<FormDialog, bool>(nameof(HasScreen));

    public event EventHandler? Confirmed;

    public event EventHandler? Cancelled;

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public FormScreen? Screen
    {
        get => GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    public string ConfirmLabel
    {
        get => GetValue(ConfirmLabelProperty);
        set => SetValue(ConfirmLabelProperty, value);
    }

    public bool HasMessage
    {
        get => GetValue(HasMessageProperty);
        private set => SetValue(HasMessageProperty, value);
    }

    public bool HasScreen
    {
        get => GetValue(HasScreenProperty);
        private set => SetValue(HasScreenProperty, value);
    }

    public void Confirm() => Confirmed?.Invoke(this, EventArgs.Empty);

    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Confirm") is { } confirm)
        {
            confirm.Click += (_, _) => Confirm();
        }

        if (e.NameScope.Find<Button>("PART_Cancel") is { } cancel)
        {
            cancel.Click += (_, _) => Cancel();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MessageProperty)
        {
            HasMessage = !string.IsNullOrEmpty(Message);
        }
        else if (change.Property == ScreenProperty)
        {
            HasScreen = Screen is not null;
        }
    }
}
