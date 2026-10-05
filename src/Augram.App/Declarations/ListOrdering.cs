namespace Augram.App.Declarations;

public enum ListOrdering
{
    /// <summary>Rows appear in the order the source gives them (the source sorts).</summary>
    Sorted,

    /// <summary>The user may drag rows into an order the source persists.</summary>
    UserOrdered,
}
