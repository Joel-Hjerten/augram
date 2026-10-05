using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

/// <summary>Builds the control for one <see cref="ScreenDeclaration"/> shape. Found by assembly scan, like field renderers.</summary>
public interface IScreenRenderer
{
    Type ScreenType { get; }

    Control Build(ScreenDeclaration screen);
}
