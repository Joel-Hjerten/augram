namespace Augram.Import.StrokesPlus;

/// <summary>
/// Member names of the StrokesPlus.net JSON document, in one place
/// (<c>docs/reference/strokesplus-net-config.md</c> §2). Readers use these and nothing else.
/// </summary>
public static class StrokesPlusJson
{
    public const string Gestures = "Gestures";
    public const string GlobalApplication = "GlobalApplication";
    public const string Applications = "Applications";
    public const string IgnoredApplications = "IgnoredApplications";
    public const string Actions = "Actions";

    /// <summary>Members of a <c>Gesture</c> element.</summary>
    public static class Gesture
    {
        public const string Name = "Name";
        public const string Active = "Active";
        public const string MatchCount = "MatchCount";
        public const string PointPatterns = "PointPatterns";
        public const string MultiPointPatterns = "MultiPointPatterns";
    }

    /// <summary>Members of a <c>PointPattern</c> element.</summary>
    public static class PointPattern
    {
        public const string Order = "Order";
        public const string Points = "Points";
    }

    /// <summary>Members of a <c>Point</c> element.</summary>
    public static class Point
    {
        public const string X = "X";
        public const string Y = "Y";
    }
}
