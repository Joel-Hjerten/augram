namespace Augram.App.Declarations;

/// <summary>
/// What a navigation entry shows. The derived records in this folder are the screen shapes; each has a
/// renderer registered in <c>Components/ScreenHost/ScreenRendererRegistry</c>.
/// </summary>
public abstract record ScreenDeclaration(string Title, string File, int Line)
{
    public SourceLocation Source => new(File, Line);
}
