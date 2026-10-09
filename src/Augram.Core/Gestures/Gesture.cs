using System.Text.Json.Serialization;

namespace Augram.Core.Gestures;

/// <summary>
/// A named gesture and the training samples that define it. Inactive gestures are kept
/// in the library but never recognised. Immutable: a change is a new record (ADR-0002 §6).
/// </summary>
public sealed record Gesture(GestureId Id, string Name, bool IsActive, IReadOnlyList<GestureSample> Samples)
{
    /// <summary>
    /// The samples as drawn, kept while <see cref="Samples"/> holds their cleaned shape (shape cleanup, plan 0001 M2 step 10;
    /// Joel 2026-10-08: the original can always be restored); null when the gesture is as drawn. Written to the file and
    /// the sync item only when set (<c>originalSamples</c>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<GestureSample>? OriginalSamples { get; init; }

    /// <summary>True while the samples are a cleaned shape with the drawn ones kept beside them.</summary>
    [JsonIgnore]
    public bool IsCleanedUp => OriginalSamples is not null;
}
