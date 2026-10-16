using System.Globalization;
using Augram.App.Components.Parts;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Engine.Hosting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;

namespace Augram.App.Components.Capture;

/// <summary>
/// The hotkey capture field (F5): shows the combination ("Ctrl+Shift+T", "RAlt+F9" when a modifier was held
/// on the right side only, see <see cref="RightHand"/>), and while capturing shows what is held live
/// ("Press a combination…" until then). Parts: <c>PART_Field</c> (clicking it captures),
/// <c>PART_Capture</c>, <c>PART_Accept</c>, <c>PART_Clear</c>; <c>:capturing</c> while capturing.
/// Capturing arms <see cref="IKeyCapture"/> (from <see cref="KeyCapture"/>, else the resource
/// <see cref="ComponentPorts.KeyCapture"/> the composition root publishes), so every key goes here and none to
/// the OS; without one it falls back to this window's key events and <see cref="StatusText"/> says what that
/// cannot record. Commit is mouse-only, since Escape is a key like any other: Accept, a press anywhere
/// outside the field, or the window losing activation keep the latest combination and raise
/// <see cref="Committed"/>; Clear commits "no key". Every way out gives the keyboard back first: those
/// three, Clear, being unloaded (keeps nothing), and the engine's own release (idle watchdog, hook reset). The text is
/// <see cref="HotkeyText"/>'s, so a key reads by what it prints under the active layout, or under the layout <see cref="LayoutId"/>
/// names (a per-layout row), and is re-read on a resource change (the composition root re-publishes <see cref="ComponentPorts"/>
/// when the layout changes; plan 0011 decision 6).
/// <see cref="SingleKey"/> is the one-key mode (a hold key, a hold remap command's input; <c>HotkeyCaptureBox.SingleKey.cs</c>).
/// </summary>
public sealed partial class HotkeyCaptureBox : TemplatedControl
{
    public const string PromptText = "Press a combination…";
    public const string EmptyText = "No key set";
    public const string EngineHelpText = "Every key comes here, Win+L and Alt+Tab included. Accept, or click anywhere else, to keep it; the mouse works as usual.";
    public const string WindowOnlyText = "The engine is off, so only keys this window receives are recorded: not Win+L, Alt+Tab or PrintScreen.";

    public static readonly StyledProperty<KeyModifiers> ModifiersProperty = AvaloniaProperty.Register<HotkeyCaptureBox, KeyModifiers>(nameof(Modifiers));

    public static readonly StyledProperty<KeyModifiers> RightHandProperty = AvaloniaProperty.Register<HotkeyCaptureBox, KeyModifiers>(nameof(RightHand));

    public static readonly StyledProperty<KeyCode> KeyProperty = AvaloniaProperty.Register<HotkeyCaptureBox, KeyCode>(nameof(Key));

    public static readonly StyledProperty<IKeyCapture?> KeyCaptureProperty = AvaloniaProperty.Register<HotkeyCaptureBox, IKeyCapture?>(nameof(KeyCapture));

    public static readonly StyledProperty<TimeSpan> IdleTimeoutProperty =
        AvaloniaProperty.Register<HotkeyCaptureBox, TimeSpan>(nameof(IdleTimeout), EngineHost.DefaultKeyCaptureIdleTimeout);

    public static readonly StyledProperty<string?> LayoutIdProperty = AvaloniaProperty.Register<HotkeyCaptureBox, string?>(nameof(LayoutId));

    public static readonly StyledProperty<IKeyboardLayouts?> KeyboardLayoutsProperty = AvaloniaProperty.Register<HotkeyCaptureBox, IKeyboardLayouts?>(nameof(KeyboardLayouts));

    public static readonly StyledProperty<bool> IsCapturingProperty = AvaloniaProperty.Register<HotkeyCaptureBox, bool>(nameof(IsCapturing));

    public static readonly StyledProperty<string> DisplayTextProperty = AvaloniaProperty.Register<HotkeyCaptureBox, string>(nameof(DisplayText), EmptyText);

    public static readonly StyledProperty<string> StatusTextProperty = AvaloniaProperty.Register<HotkeyCaptureBox, string>(nameof(StatusText), string.Empty);

    public static readonly StyledProperty<bool> HasStatusProperty = AvaloniaProperty.Register<HotkeyCaptureBox, bool>(nameof(HasStatus));

    private readonly HotkeyRecorder _recorder = new();
    private IDisposable? _engineCapture;
    private CaptureWindowWatch? _watch;
    private int _session;

    public event EventHandler<HotkeyCommittedEventArgs>? Committed;

    public HotkeyCaptureBox()
    {
        // The key names follow the active keyboard layout (plan 0011 decision 6): the composition root re-publishes the ports
        // resource when it changes, and that reaches this field as a resource change, so the text is re-read.
        ResourcesChanged += (_, _) => Refresh(StatusText);
    }

    public KeyModifiers Modifiers { get => GetValue(ModifiersProperty); set => SetValue(ModifiersProperty, value); }

    /// <summary>Which of <see cref="Modifiers"/> are the right-hand key; bits outside <see cref="Modifiers"/> show nothing.</summary>
    public KeyModifiers RightHand { get => GetValue(RightHandProperty); set => SetValue(RightHandProperty, value); }

    public KeyCode Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <summary>Explicit capture service (tests, gallery); null looks up <see cref="ComponentPorts.KeyCapture"/>.</summary>
    public IKeyCapture? KeyCapture { get => GetValue(KeyCaptureProperty); set => SetValue(KeyCaptureProperty, value); }

    /// <summary>F5's watchdog: with no key event for this long the engine releases the keyboard. Default 10 s.</summary>
    public TimeSpan IdleTimeout { get => GetValue(IdleTimeoutProperty); set => SetValue(IdleTimeoutProperty, value); }

    /// <summary>
    /// The keyboard layout the key is named by (a <see cref="KeyboardLayout.Id"/>; a per-layout row's field in the Hotkey form,
    /// so the Swedish row reads "Cmd+Å" and the ABC row "Cmd+[" whatever is active); null, the default, names by the active one.
    /// </summary>
    public string? LayoutId { get => GetValue(LayoutIdProperty); set => SetValue(LayoutIdProperty, value); }

    /// <summary>
    /// Explicit layouts port the key names ask (the Hotkey form hands its own to each row; tests, gallery); null looks up
    /// <see cref="ComponentPorts.KeyboardLayouts"/>, and without one <see cref="HotkeyText"/>'s own (the adapter the App set at startup).
    /// </summary>
    public IKeyboardLayouts? KeyboardLayouts { get => GetValue(KeyboardLayoutsProperty); set => SetValue(KeyboardLayoutsProperty, value); }

    public bool IsCapturing { get => GetValue(IsCapturingProperty); private set => SetValue(IsCapturingProperty, value); }

    public string DisplayText { get => GetValue(DisplayTextProperty); private set => SetValue(DisplayTextProperty, value); }

    /// <summary>One line under the field: what capturing does, the no-engine limits, or why a capture ended.</summary>
    public string StatusText { get => GetValue(StatusTextProperty); private set => SetValue(StatusTextProperty, value); }

    public bool HasStatus { get => GetValue(HasStatusProperty); private set => SetValue(HasStatusProperty, value); }

    /// <summary>True while the window's own key events are the source (no engine).</summary>
    public bool IsWindowOnly { get; private set; }

    /// <summary>Starts capturing; does nothing while already capturing or before the field is in a window.</summary>
    public void BeginCapture()
    {
        if (IsCapturing || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        _recorder.Reset();
        var session = ++_session;
        var service = KeyCapture ?? ComponentPorts.Near(this)?.KeyCapture;
        _engineCapture = service?.Begin(captured => OnCaptured(session, captured), IdleTimeout);
        IsWindowOnly = _engineCapture is null;
        _watch = new CaptureWindowWatch(topLevel, this, IsWindowOnly, captured => OnCaptured(session, captured), Accept);
        IsCapturing = true;
        PseudoClasses.Set(":capturing", true);
        Refresh(CaptureHelp);
    }

    /// <summary>Keeps the latest combination (if any key was pressed) and gives the keyboard back.</summary>
    public void Accept() => End(keep: true, status: string.Empty);

    /// <summary>Ends any capture, then commits "no key".</summary>
    public void Clear()
    {
        End(keep: false, status: string.Empty);
        Modifiers = KeyModifiers.None;
        RightHand = KeyModifiers.None;
        Key = KeyCode.None;
        Refresh(string.Empty);
        Committed?.Invoke(this, new HotkeyCommittedEventArgs(KeyModifiers.None, KeyCode.None, KeyModifiers.None));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        e.OnClick("PART_Capture", BeginCapture);
        e.OnClick("PART_Accept", Accept);
        e.OnClick("PART_Clear", Clear);
        if (e.NameScope.Find<Control>("PART_Field") is { } field)
        {
            field.PointerPressed += (_, _) => BeginCapture();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModifiersProperty || change.Property == RightHandProperty || change.Property == KeyProperty || change.Property == SingleKeyProperty || change.Property == LayoutIdProperty || change.Property == KeyboardLayoutsProperty)
        {
            Refresh(StatusText);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        End(keep: false, status: string.Empty);
    }


    private static string EndedText(string? reason, TimeSpan idle) => reason == KeyCaptureEvent.IdleTimeoutReason
        ? string.Create(CultureInfo.InvariantCulture, $"Capture stopped after {idle.TotalSeconds:0.#} s without a key.")
        : $"Capture stopped ({reason}).";

    private void OnCaptured(int session, KeyCaptureEvent captured)
    {
        if (session != _session || !IsCapturing)
        {
            return;
        }

        switch (captured.Kind)
        {
            case KeyCaptureEventKind.KeyDown when SingleKey && Refusal(captured.Key) is { } why:
                // One-key mode (a hold key, an input): a refused key is not recorded, and the field says why.
                Refresh(why);
                return;
            case KeyCaptureEventKind.KeyDown when SingleKey && AcceptsModifier && HotkeyKeys.IsModifier(captured.Key):
                // A modifier alone is the key here (plan 0012); the last key pressed wins.
                _recorder.Alone(captured.Key);
                Refresh(CaptureHelp);
                return;
            case KeyCaptureEventKind.KeyDown:
                _recorder.Down(captured.Key, captured.Modifiers);
                if (SingleKey && _recorder.HasCombination)
                {
                    Refresh(CaptureHelp);
                    return;
                }

                break;
            case KeyCaptureEventKind.KeyUp:
                _recorder.Up(captured.Key);
                break;
            case KeyCaptureEventKind.Released:
                End(keep: true, status: EndedText(captured.Reason, IdleTimeout));
                return;
        }

        Refresh(StatusText);
    }

    /// <summary>The one exit: keyboard back first, window hooks off, then the commit, then the text.</summary>
    private void End(bool keep, string status)
    {
        if (!IsCapturing)
        {
            return;
        }

        _session++;
        _engineCapture?.Dispose();
        _engineCapture = null;
        _watch?.Dispose();
        _watch = null;
        IsWindowOnly = false;
        IsCapturing = false;
        PseudoClasses.Set(":capturing", false);
        if (keep && _recorder.HasCombination)
        {
            // One-key mode keeps the key alone, whatever modifiers were held with it.
            var modifiers = SingleKey ? KeyModifiers.None : _recorder.Modifiers;
            var rightHand = SingleKey ? KeyModifiers.None : _recorder.RightHand;
            Modifiers = modifiers;
            RightHand = rightHand;
            Key = _recorder.Key;
            Committed?.Invoke(this, new HotkeyCommittedEventArgs(modifiers, _recorder.Key, rightHand));
        }

        Refresh(status);
    }

    /// <summary>Whose layouts name the key: <see cref="KeyboardLayouts"/>, else the published port, else null (<see cref="HotkeyText"/>'s own).</summary>
    private IKeyboardLayouts? Layouts => KeyboardLayouts ?? ComponentPorts.Near(this)?.KeyboardLayouts;

    private void Refresh(string status)
    {
        DisplayText = IsCapturing ? LiveText()
            : Key == KeyCode.None ? EmptyText
            : SingleKey ? HotkeyText.KeyName(Key, layouts: Layouts, layoutId: LayoutId)
            : HotkeyText.Format(Modifiers, Key, RightHand, layouts: Layouts, layoutId: LayoutId);
        StatusText = status;
        HasStatus = status.Length > 0;
    }
}
