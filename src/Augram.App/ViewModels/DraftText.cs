using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// A text field that edits a stored value as a draft and commits it on leaving or Enter, not per keystroke
/// (Options › Sync's repository address and machine name, rendered by <c>OptionsSyncSection.CommittedText</c>).
/// <see cref="Text"/> is the value as typed and each keystroke re-checks it (<see cref="Problem"/>, shown under the box);
/// <see cref="Commit"/> checks the trimmed text, keeps the problem on show and stores nothing while there is one, and
/// otherwise hands the value (null for blank) to the store when it differs from the stored one; <see cref="Revert"/>
/// (Escape) puts the stored value back; <see cref="Follow"/>, called on every store change, replaces the draft only when
/// the stored value itself changed, so a draft being typed survives unrelated changes.
/// </summary>
public sealed class DraftText : ObservableObject
{
    private readonly Func<string?> _stored;
    private readonly Func<string, string?> _problem;
    private readonly Action<string?> _commit;
    private string? _seen;
    private string _text;

    /// <param name="stored">The stored value; null shows as an empty box.</param>
    /// <param name="problem">Why a text cannot be stored, or null; given the text as typed, and trimmed on commit.</param>
    /// <param name="commit">Stores a checked value that differs from the stored one (null for a blank text).</param>
    public DraftText(Func<string?> stored, Func<string, string?> problem, Action<string?> commit)
    {
        _stored = stored ?? throw new ArgumentNullException(nameof(stored));
        _problem = problem ?? throw new ArgumentNullException(nameof(problem));
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
        _seen = stored();
        _text = _seen ?? string.Empty;
    }

    /// <summary>The value as typed; committed by <see cref="Commit"/>.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                Problem = _problem(_text);
            }
        }
    }

    /// <summary>Why the typed text cannot be used, or null. The owner may also show the store's own refusal here.</summary>
    public string? Problem
    {
        get;
        internal set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ProblemText));
            }
        }
    }

    public string ProblemText => Problem ?? string.Empty;

    /// <summary>Stores the trimmed text when it has no problem and differs from the stored value; a problem stays on show and nothing changes.</summary>
    public void Commit()
    {
        var text = _text.Trim();
        if (_problem(text) is { } problem)
        {
            Problem = problem;
            return;
        }

        Problem = null;
        var value = text.Length == 0 ? null : text;
        if (!string.Equals(value, _stored(), StringComparison.Ordinal))
        {
            _commit(value);
        }
    }

    /// <summary>Puts the stored value back and clears the problem.</summary>
    public void Revert()
    {
        Text = _stored() ?? string.Empty;
        Problem = null;
    }

    /// <summary>On a store change: a stored value that changed (elsewhere, or just committed) replaces the draft; one being typed stays.</summary>
    public void Follow()
    {
        var stored = _stored();
        if (!string.Equals(stored, _seen, StringComparison.Ordinal))
        {
            _seen = stored;
            Text = stored ?? string.Empty;
            Problem = null;
        }
    }
}
