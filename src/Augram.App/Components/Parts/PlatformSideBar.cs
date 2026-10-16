using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.Parts;

/// <summary>
/// Lookless "… for: Windows · macOS" row (plan 0010; plan 0012), the first row of a per-platform list's card: a view switch
/// (<c>PART_ForWindows</c>, <c>PART_ForMac</c>, the "Fields for" switch's segmented look, this computer's segment outlined with
/// <c>here</c>) that asks the host to show that platform's list, Copy from the other platform (<c>PART_Copy</c>), Mark as
/// checked (<c>PART_MarkChecked</c>) and a note, Copy first so it never moves (Joel, 0.14.2: one line). <see cref="Label"/> and
/// <see cref="Help"/> name the list ("Steps for", "Ignore for"); <see cref="Side"/> is what it shows, null hides the row. It
/// only asks (<see cref="SideRequested"/>, <see cref="CopyRequested"/>, <see cref="MarkCheckedRequested"/>) and then shows its
/// <see cref="Side"/> again. The step list's is <c>CommandHeader/StepSideBar</c>, the While holding inputs' is
/// <c>HeldInputList</c>'s.
/// </summary>
public class PlatformSideBar : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PlatformSideBar, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string?> HelpProperty =
        AvaloniaProperty.Register<PlatformSideBar, string?>(nameof(Help));

    public static readonly StyledProperty<PlatformSide?> SideProperty =
        AvaloniaProperty.Register<PlatformSideBar, PlatformSide?>(nameof(Side));

    public static readonly StyledProperty<bool> HasSideProperty =
        AvaloniaProperty.Register<PlatformSideBar, bool>(nameof(HasSide));

    public static readonly StyledProperty<string?> NoteTextProperty =
        AvaloniaProperty.Register<PlatformSideBar, string?>(nameof(NoteText));

    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<PlatformSideBar, string?>(nameof(CopyText));

    public static readonly StyledProperty<bool> CanMarkCheckedProperty =
        AvaloniaProperty.Register<PlatformSideBar, bool>(nameof(CanMarkChecked));

    private RadioButton? _forWindows;
    private RadioButton? _forMac;
    private bool _applying;

    /// <summary>The switch asks to show that platform's list.</summary>
    public event EventHandler<HostPlatform>? SideRequested;

    /// <summary>Copy from the other platform was clicked.</summary>
    public event EventHandler? CopyRequested;

    /// <summary>Mark as checked was clicked.</summary>
    public event EventHandler? MarkCheckedRequested;

    /// <summary>The row's label: "Steps for", "Ignore for".</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The label's ⓘ; null shows none.</summary>
    public string? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    /// <summary>What the row shows; null hides it.</summary>
    public PlatformSide? Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>A side is set: the row shows.</summary>
    public bool HasSide
    {
        get => GetValue(HasSideProperty);
        private set => SetValue(HasSideProperty, value);
    }

    /// <summary>The note after the buttons (<see cref="PlatformSide.Note"/>).</summary>
    public string? NoteText
    {
        get => GetValue(NoteTextProperty);
        private set => SetValue(NoteTextProperty, value);
    }

    /// <summary>"Copy from Windows"; null hides the button.</summary>
    public string? CopyText
    {
        get => GetValue(CopyTextProperty);
        private set => SetValue(CopyTextProperty, value);
    }

    /// <summary>Mark as checked shows.</summary>
    public bool CanMarkChecked
    {
        get => GetValue(CanMarkCheckedProperty);
        private set => SetValue(CanMarkCheckedProperty, value);
    }

    /// <summary>What the switch does: asks the host to show <paramref name="platform"/>'s list; the switch then shows the answer.</summary>
    public void ShowSide(HostPlatform platform)
    {
        if (Side is { } side && side.Shown != platform)
        {
            SideRequested?.Invoke(this, platform);
        }

        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _forWindows = e.NameScope.Find<RadioButton>("PART_ForWindows");
        _forMac = e.NameScope.Find<RadioButton>("PART_ForMac");
        Wire(_forWindows, HostPlatform.Windows);
        Wire(_forMac, HostPlatform.MacOS);
        e.OnClick("PART_Copy", () => CopyRequested?.Invoke(this, EventArgs.Empty));
        e.OnClick("PART_MarkChecked", () => MarkCheckedRequested?.Invoke(this, EventArgs.Empty));
        Apply();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty)
        {
            var side = Side;
            HasSide = side is not null;
            NoteText = side?.Note;
            CopyText = side?.CopyText;
            CanMarkChecked = side is { CanMarkChecked: true };
            Apply();
        }
    }

    private void Apply()
    {
        _applying = true;
        try
        {
            var side = Side;
            var shown = side?.Shown;
            // This computer's platform is outlined whichever side is in view (Joel, 0.14.1), as on the "Fields for" switch.
            var here = side is null ? (HostPlatform?)null : side.IsHere ? side.Shown : side.Shown.Other();
            if (_forWindows is not null)
            {
                _forWindows.IsChecked = shown == HostPlatform.Windows;
                _forWindows.Classes.Set("here", here == HostPlatform.Windows);
            }

            if (_forMac is not null)
            {
                _forMac.IsChecked = shown == HostPlatform.MacOS;
                _forMac.Classes.Set("here", here == HostPlatform.MacOS);
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
                    ShowSide(platform);
                }
            };
        }
    }
}
