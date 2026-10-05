using Augram.App.Declarations;
using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

/// <summary>Shows whatever control <see cref="ScreenRendererRegistry"/> builds for a <see cref="ScreenDeclaration"/>.</summary>
public sealed class ScreenHost : ContentControl
{
    public static readonly StyledProperty<ScreenDeclaration?> ScreenProperty =
        AvaloniaProperty.Register<ScreenHost, ScreenDeclaration?>(nameof(Screen));

    public ScreenDeclaration? Screen
    {
        get => GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    public ScreenRendererRegistry Renderers { get; init; } = ScreenRendererRegistry.Default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScreenProperty)
        {
            Content = Screen is null ? null : Renderers.Build(Screen);
        }
    }
}
