using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

public sealed class FormScreenRenderer : IScreenRenderer
{
    public Type ScreenType => typeof(FormScreen);

    public Control Build(ScreenDeclaration screen) => new SectionForm.SectionForm { Screen = (FormScreen)screen };
}
