namespace Augram.Core.Gestures;

/// <summary>
/// The business rules of the gesture library (ADR-0002 §5a: one home, called by the store;
/// view models only display the outcome). Names compare trimmed and case-insensitively.
/// </summary>
public static class GestureRules
{
    public static StringComparer NameComparer { get; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>Trims the name and forces a sample-less placeholder inactive.</summary>
    public static Gesture Normalised(Gesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        var name = gesture.Name?.Trim() ?? string.Empty;
        bool isActive = gesture.IsActive && gesture.Samples.Count > 0;
        return gesture with { Name = name, IsActive = isActive };
    }

    /// <summary>True when the gesture has at least one sample a recognizer can use.</summary>
    public static bool HasUsableSample(Gesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return gesture.Samples.Any(sample => sample.Distinct().Skip(1).Any());
    }

    /// <summary>Checks a normalised gesture against the others it will sit beside.</summary>
    public static void EnsureValid(Gesture gesture, IEnumerable<Gesture> others)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(others);

        if (gesture.Name.Length == 0)
        {
            throw new GestureValidationException("A gesture needs a name.");
        }

        if (gesture.Samples.Count > 0 && !HasUsableSample(gesture))
        {
            throw new GestureValidationException($"'{gesture.Name}' needs at least one sample with two distinct points.");
        }

        foreach (var other in others)
        {
            if (other.Id == gesture.Id)
            {
                throw new GestureValidationException($"A gesture with id {gesture.Id} already exists.");
            }

            if (NameComparer.Equals(other.Name, gesture.Name))
            {
                throw new GestureValidationException($"A gesture named '{other.Name}' already exists.");
            }
        }
    }

    /// <summary>Normalises and validates a whole set (a load or an import), in order.</summary>
    public static Gesture[] ValidSet(IEnumerable<Gesture> gestures)
    {
        ArgumentNullException.ThrowIfNull(gestures);

        var accepted = new List<Gesture>();
        foreach (var gesture in gestures)
        {
            var normalised = Normalised(gesture);
            EnsureValid(normalised, accepted);
            accepted.Add(normalised);
        }

        return accepted.ToArray();
    }
}
