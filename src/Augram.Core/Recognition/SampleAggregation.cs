namespace Augram.Core.Recognition;

/// <summary>How the per-sample scores of a multi-sample gesture combine into the gesture's score.</summary>
public enum SampleAggregation
{
    /// <summary>Mean of the per-sample scores, as StrokesPlus classic does.</summary>
    Average,

    /// <summary>Highest per-sample score wins ("highest match probability", as Rob described SP.net).</summary>
    Best,
}
