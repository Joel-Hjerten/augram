namespace Augram.App.Declarations;

/// <summary>Maps a typed binding onto choice indexes for <see cref="IChoiceField"/> renderers.</summary>
public sealed class ChoiceBinding<T> : IChoiceField
{
    private readonly IReadOnlyList<Choice<T>> _choices;
    private readonly IValueBinding<T> _value;

    public ChoiceBinding(IReadOnlyList<Choice<T>> choices, IValueBinding<T> value)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(value);
        _choices = choices;
        _value = value;
        Labels = [.. choices.Select(choice => choice.Label)];
        _value.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<string> Labels { get; }

    public bool IsReadOnly => _value.IsReadOnly;

    public int SelectedIndex
    {
        get
        {
            var current = _value.Get();
            for (var i = 0; i < _choices.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(_choices[i].Value, current))
                {
                    return i;
                }
            }

            return -1;
        }
        set
        {
            if (value >= 0 && value < _choices.Count)
            {
                _value.Set(_choices[value].Value);
            }
        }
    }
}
