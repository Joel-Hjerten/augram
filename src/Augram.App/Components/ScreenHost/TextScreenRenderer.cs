using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

public sealed class TextScreenRenderer : IScreenRenderer
{
    public Type ScreenType => typeof(TextScreen);

    public Control Build(ScreenDeclaration screen)
    {
        var text = (TextScreen)screen;
        var panel = new TextPanel.TextPanel { Text = text.Text };
        Region.Mark(panel, text.Title, new RegionInfo(text.Title, "Text", text.Source));
        return panel;
    }
}
