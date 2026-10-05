using Avalonia;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.TextPanel;

/// <summary>Lookless block of wrapped text: placeholder tabs and explanations.</summary>
public sealed class TextPanel : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<TextPanel, string>(nameof(Text), string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
