using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>
/// Caches each training sample's resampled angle sequence per precision, keyed by
/// gesture id, sample index and precision. An entry also remembers the
/// <see cref="GestureSample"/> instance it was computed from; because samples are
/// immutable, a different instance under the same key means the gesture was retrained
/// and the entry is recomputed. Safe to share between threads.
/// </summary>
public sealed class TemplateCache
{
    private readonly Dictionary<(Guid GestureId, int SampleIndex, int Precision), Entry> _entries = [];
    private readonly Lock _gate = new();

    public double[] GetAngles(GestureId gestureId, int sampleIndex, GestureSample sample, int precision)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var key = (gestureId.Value, sampleIndex, precision);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry) && ReferenceEquals(entry.Sample, sample))
            {
                return entry.Angles;
            }

            var angles = AngleSequence.FromPoints(StrokeResampler.Resample(sample, precision));
            _entries[key] = new Entry(sample, angles);
            return angles;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private readonly record struct Entry(GestureSample Sample, double[] Angles);
}
