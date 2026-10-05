namespace Augram.App.Declarations;

/// <summary>Implemented by the generic choice fields so a renderer can reach their <see cref="IChoiceField"/> without knowing <c>T</c>.</summary>
public interface IChoiceSource
{
    IChoiceField AsChoices();
}
