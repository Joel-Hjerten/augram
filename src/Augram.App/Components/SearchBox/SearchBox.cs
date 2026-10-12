using Augram.App.Components.Parts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Augram.App.Components.SearchBox;

/// <summary>
/// The one search box of every list's toolbar (plan 0009 decision 1): <see cref="Text"/> (two-way; the host filters on every
/// change, decision 6) with <see cref="Placeholder"/> as its watermark, in the template's <c>PART_Text</c>, and a clear glyph
/// (<c>PART_Clear</c>, shown while there is text). Escape in the box clears it (swallowed, so a dialog around it stays
/// open); in an empty box it leaves for the list's selected row and passes on. <c>Focus()</c> lands in the text with it selected: the lists' find key
/// (<c>EditKeymap.Find</c>) calls it. The matching rule is <see cref="SearchQuery"/>; the box only holds the text.
/// </summary>
public sealed class SearchBox : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SearchBox, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<SearchBox, string>(nameof(Placeholder), "Search");

    private TextBox? _text;
    private Button? _clear;

    public SearchBox()
    {
        // Focusable so that Focus() works; the text box inside is the tab stop.
        Focusable = true;
        IsTabStop = false;
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>True while the keyboard focus is in the text: a list's edit keys stand down then, so Delete and Enter edit the text.</summary>
    public bool IsTyping => _text?.IsKeyboardFocusWithin == true;

    public void Clear() => Text = string.Empty;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<TextBox>("PART_Text");
        if (_text is not null)
        {
            _text.KeyDown += OnTextKeyDown;
        }

        // Shown while there is text: set here, since a binding inside the text box's inner content does not reach this box.
        _clear = e.OnClick("PART_Clear", () =>
        {
            Clear();
            _text?.Focus();
        });
        ShowClear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            ShowClear();
        }
    }

    private void ShowClear()
    {
        if (_clear is not null)
        {
            _clear.IsVisible = Text.Length > 0;
        }
    }

    /// <summary>Focus given to the box itself (Focus(), the find key) goes on to the text, selected so typing replaces it.</summary>
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this) && _text is { } text)
        {
            text.Focus();
            text.SelectAll();
        }
    }

    /// <summary>
    /// Escape in two steps (Joel, 0.13.13): with text it clears it and stays, swallowed so a dialog around the box stays open;
    /// in an empty box it leaves for the list the box belongs to, so its keys work at once, and passes on (the gesture picker
    /// closes, as before).
    /// </summary>
    private void OnTextKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (Text.Length > 0)
        {
            Clear();
            e.Handled = true;
            return;
        }

        LeaveToList();
    }

    /// <summary>The box sits in its list's template: focus that list's selected row, else the rows, else nothing at all.</summary>
    private void LeaveToList()
    {
        var rows = (TemplatedParent as Visual)?.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
        var row = rows?.SelectedItem is { } selected ? rows.ContainerFromItem(selected) : null;
        if (row?.Focus() == true || rows?.Focus() == true)
        {
            return;
        }

        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }
}
