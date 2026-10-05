namespace Augram.App.Declarations;

/// <summary>
/// What a choice renderer (dropdown, button radio) needs without knowing the value type: labels and a
/// selected index that maps onto the generic binding. Implemented by <see cref="DropdownField{T}"/>
/// and <see cref="ButtonRadioField{T}"/> through <see cref="ChoiceBinding{T}"/>.
/// </summary>
public interface IChoiceField
{
    IReadOnlyList<string> Labels { get; }

    /// <summary>-1 when the bound value matches no choice.</summary>
    int SelectedIndex { get; set; }

    bool IsReadOnly { get; }

    event EventHandler? Changed;
}
