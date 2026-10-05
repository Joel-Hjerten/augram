namespace Augram.App.Declarations;

/// <summary>Rows for an <see cref="ListSpec"/>. <see cref="Changed"/> may fire on any thread; the renderer marshals.</summary>
public interface IListSource
{
    IReadOnlyList<object> Items { get; }

    event EventHandler? Changed;
}
