using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

public sealed class ComponentScreenRenderer : IScreenRenderer
{
    public Type ScreenType => typeof(ComponentScreen);

    public Control Build(ScreenDeclaration screen)
    {
        var component = (ComponentScreen)screen;
        var control = component.Build();
        Region.Mark(control, component.Title, new RegionInfo(component.Title, "Component", component.Source));
        return control;
    }
}
