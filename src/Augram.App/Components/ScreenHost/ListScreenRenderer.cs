using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

public sealed class ListScreenRenderer : IScreenRenderer
{
    public Type ScreenType => typeof(ListScreen);

    public Control Build(ScreenDeclaration screen) => new ItemList.ItemList { Spec = ((ListScreen)screen).Spec };
}
