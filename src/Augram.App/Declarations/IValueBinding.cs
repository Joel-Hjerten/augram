namespace Augram.App.Declarations;

/// <summary>
/// The abstraction a declared field binds through (ADR-0002 §5c). A field never touches a view model
/// directly; it reads and writes through this, so the same declaration can be bound to a store, a
/// placeholder view model, or gallery fake data. The non-generic base is what the inspector reports
/// and what a renderer observes.
/// </summary>
public interface IValueBinding
{
    /// <summary>The view-model property this binding reads, for the F1 inspector; null when unknown.</summary>
    string? PropertyName { get; }

    bool IsReadOnly { get; }

    /// <summary>Raised on the thread that changed the value; renderers marshal to the UI thread themselves.</summary>
    event EventHandler? Changed;
}

public interface IValueBinding<T> : IValueBinding
{
    T Get();

    void Set(T value);
}
