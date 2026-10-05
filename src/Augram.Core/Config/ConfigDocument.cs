using Augram.Core.Gestures;

namespace Augram.Core.Config;

/// <summary>
/// The whole on-disk configuration (requirements F8): one JSON file, <c>schemaVersion</c>
/// first, settings, then the gesture library. Export uses the same shape. Immutable; the
/// running app rebuilds it from the stores (<see cref="ConfigSession.Document"/>). The
/// constructor parameters are optional so a missing section takes its default on read.
/// </summary>
public sealed record ConfigDocument
{
    /// <summary>The version this build writes and the highest it can read.</summary>
    public const int CurrentSchemaVersion = 1;

    public ConfigDocument(int schemaVersion = CurrentSchemaVersion, Settings? settings = null, IReadOnlyList<Gesture>? gestures = null)
    {
        SchemaVersion = schemaVersion;
        Settings = settings ?? Settings.Default;
        Gestures = gestures ?? [];
    }

    public int SchemaVersion { get; init; }

    public Settings Settings { get; init; }

    public IReadOnlyList<Gesture> Gestures { get; init; }

    /// <summary>A fresh install: default settings and the starter gestures (plan 0001 C3).</summary>
    public static ConfigDocument Default { get; } = new() { Gestures = StarterGestures.All() };
}
