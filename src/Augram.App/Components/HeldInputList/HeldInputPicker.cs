using Augram.App.Components.Capture;
using Augram.App.Components.Parts;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;
using MouseButton = Augram.Core.Capture.MouseButton;

namespace Augram.App.Components.HeldInputList;

/// <summary>
/// Lookless "New…" line of the <see cref="HeldInputList"/> (plan 0012 decision 2): one input, by recording or by picking.
/// <c>PART_Key</c> is the hotkey capture field in one-key mode that takes a modifier pressed alone
/// (<see cref="HotkeyCaptureBox.AcceptsModifier"/>; Accept or a click elsewhere keeps the key, as in every capture field);
/// <c>PART_Button</c> listens for the next mouse button press (<see cref="ButtonDetector"/>, the hold remap's Add button…'s);
/// <c>PART_Pick</c> is a drop-down of the mouse buttons and the modifiers (<see cref="Choices"/>, awkward to record alone), an
/// <see cref="AskingDropdown"/>; <c>PART_Cancel</c> closes the line. Each ends in one <see cref="Picked"/> or
/// <see cref="Cancelled"/>; the list adds nothing itself.
/// </summary>
public sealed class HeldInputPicker : TemplatedControl
{
    public const string PickPlaceholder = "Pick a button or modifier…";

    public static readonly StyledProperty<HostPlatform> PlatformProperty =
        AvaloniaProperty.Register<HeldInputPicker, HostPlatform>(nameof(Platform), RunningPlatform.Current);

    public static readonly StyledProperty<string> ButtonStatusProperty =
        AvaloniaProperty.Register<HeldInputPicker, string>(nameof(ButtonStatus), string.Empty);

    public static readonly StyledProperty<bool> HasButtonStatusProperty =
        AvaloniaProperty.Register<HeldInputPicker, bool>(nameof(HasButtonStatus));

    private readonly ButtonDetector _detector;
    private HotkeyCaptureBox? _key;
    private AskingDropdown? _pick;

    public HeldInputPicker()
    {
        _detector = new ButtonDetector(this, button => Pick(new KeyOrButton.Button(button)));
        _detector.Changed += (_, _) =>
        {
            ButtonStatus = _detector.Status;
            HasButtonStatus = ButtonStatus.Length > 0;
        };
    }

    /// <summary>An input was recorded or picked.</summary>
    public event EventHandler<KeyOrButton>? Picked;

    /// <summary>Cancel was clicked.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Whose key names the drop-down reads in (Opt and Cmd on a Mac): the side being edited.</summary>
    public HostPlatform Platform
    {
        get => GetValue(PlatformProperty);
        set => SetValue(PlatformProperty, value);
    }

    /// <summary>The button detector's line: the prompt while listening, the timeout, or nothing.</summary>
    public string ButtonStatus
    {
        get => GetValue(ButtonStatusProperty);
        private set => SetValue(ButtonStatusProperty, value);
    }

    public bool HasButtonStatus
    {
        get => GetValue(HasButtonStatusProperty);
        private set => SetValue(HasButtonStatusProperty, value);
    }

    /// <summary>Explicit button capture service (tests, gallery); null looks up <see cref="ComponentPorts.ButtonCapture"/>.</summary>
    public IButtonCapture? ButtonCapture
    {
        get => _detector.Capture;
        set => _detector.Capture = value;
    }

    /// <summary>True while the key field or the button detector listens: the list's keys stand down then.</summary>
    public bool IsListening => _key?.IsCapturing == true || _detector.IsListening;

    /// <summary>The drop-down's inputs, in order: the mouse buttons, then Ctrl, Alt, Shift and Win (Cmd on a Mac).</summary>
    public static IReadOnlyList<KeyOrButton> Choices { get; } =
    [
        new KeyOrButton.Button(MouseButton.Left),
        new KeyOrButton.Button(MouseButton.Right),
        new KeyOrButton.Button(MouseButton.Middle),
        new KeyOrButton.Button(MouseButton.X1),
        new KeyOrButton.Button(MouseButton.X2),
        new KeyOrButton.Modifier(KeyModifiers.Control),
        new KeyOrButton.Modifier(KeyModifiers.Alt),
        new KeyOrButton.Modifier(KeyModifiers.Shift),
        new KeyOrButton.Modifier(KeyModifiers.Meta),
    ];

    /// <summary>What the drop-down's choice <paramref name="index"/> adds (tests pick through it as the user does).</summary>
    public void PickChoice(int index) => Pick(Choices[index]);

    /// <summary>A key the field recorded (a modifier key folds into its modifier, <see cref="KeyOrButton.FromKey"/>).</summary>
    public void PickKey(KeyCode key)
    {
        if (KeyOrButton.FromKey(key) is { } input)
        {
            Pick(input);
        }
    }

    /// <summary>Starts listening for the next mouse button press.</summary>
    public void DetectButton() => _detector.Start();

    /// <summary>Stops any listening and clears the field: the line is closed or reopened.</summary>
    public void Reset()
    {
        _detector.Stop();
        if (_key is not null)
        {
            _key.Key = KeyCode.None;
        }

        ShowPick();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _key = e.NameScope.Find<HotkeyCaptureBox>("PART_Key");
        if (_key is not null)
        {
            _key.SingleKey = true;
            _key.AcceptsModifier = true;
            _key.Committed += (_, args) => PickKey(args.Key);
        }

        if (e.NameScope.Find<ComboBox>("PART_Pick") is { } box)
        {
            _pick = new AskingDropdown(box, PickChoice);
            ShowPick();
        }

        e.OnClick("PART_Button", DetectButton);
        e.OnClick("PART_Cancel", () =>
        {
            Reset();
            Cancelled?.Invoke(this, EventArgs.Empty);
        });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlatformProperty)
        {
            ShowPick();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _detector.Stop();
    }

    private void ShowPick() => _pick?.Show([.. Choices.Select(choice => choice.Describe(Platform))], -1);

    private void Pick(KeyOrButton input)
    {
        _detector.Stop();
        ShowPick();
        if (_key is not null)
        {
            _key.Key = KeyCode.None;
        }

        Picked?.Invoke(this, input);
    }
}
