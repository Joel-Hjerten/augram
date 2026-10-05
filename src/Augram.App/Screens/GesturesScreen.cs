using Augram.App.Declarations;

namespace Augram.App.Screens;

/// <summary>Placeholder until M1 step 7 declares the gesture grid as a <see cref="ListScreen"/>.</summary>
public static class GesturesScreen
{
    public static ScreenDeclaration Declare() =>
        new TextScreen("Gestures", "Gesture list coming in step 7: one tile per gesture with its auto-generated glyph, plus the learn dialog.");
}
