using Avalonia.Controls;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// A dropdown that only asks (the <see cref="CommandHeader"/>'s trigger kind and category): a user's pick
/// goes to the callback, which asks the host and then calls <see cref="Show"/> with what is really
/// stored, so a refused or cancelled request leaves the box honest. What <see cref="Show"/> sets never
/// echoes back as a pick.
/// </summary>
internal sealed class AskingDropdown
{
    private readonly ComboBox _box;
    private IReadOnlyList<string> _labels = [];
    private bool _applying;

    public AskingDropdown(ComboBox box, Action<int> picked)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(picked);
        _box = box;
        _box.SelectionChanged += (_, _) =>
        {
            if (!_applying && _box.SelectedIndex >= 0)
            {
                picked(_box.SelectedIndex);
            }
        };
    }

    public int SelectedIndex => _box.SelectedIndex;

    /// <summary>Shows <paramref name="labels"/> (replaced only when they changed) with <paramref name="index"/> selected; -1 for none.</summary>
    public void Show(IReadOnlyList<string> labels, int index)
    {
        _applying = true;
        try
        {
            if (!_labels.SequenceEqual(labels))
            {
                _labels = labels;
                _box.ItemsSource = labels;
            }

            _box.SelectedIndex = index;
        }
        finally
        {
            _applying = false;
        }
    }
}
