using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.DisplayMode;

/// <summary>
/// The "Display mode" step type: key <c>displayMode</c>, category Display, platform-neutral (a resolution and a rate
/// mean the same on both platforms in each one's own units; the adapter resolves them). Parameters:
/// <c>{ "width": 1920, "height": 1080, "refreshHz": 119.88, "display": "UnderGesture" }</c>; <c>width</c> and
/// <c>height</c> (1..32767) come together or not at all (absent = Auto), <c>refreshHz</c> is a number above 0 and at
/// most 1000, kept to three decimals, or the string <c>"highest"</c> for the highest rate offered (absent = Auto),
/// <c>display</c> a <see cref="DisplayTarget"/> name. The default instance changes nothing until a resolution or a rate
/// is chosen.
/// </summary>
public sealed class DisplayModeStepType : IStepType
{
    public const string WidthMember = "width";

    public const string HeightMember = "height";

    public const string RefreshMember = "refreshHz";

    public const string DisplayMember = "display";

    /// <summary>The <c>refreshHz</c> value for <see cref="DisplayModeStep.HighestRefresh"/>.</summary>
    public const string HighestValue = "highest";

    private DisplayModeStepType()
    {
    }

    public static DisplayModeStepType Instance { get; } = new();

    public string Key => "displayMode";

    public string DisplayName => "Display mode";

    public StepCategory Category => StepCategory.Display;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new DisplayModeStep();

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var width = StepParameters.ReadInt32(parameters, WidthMember, DisplayResolution.MinSide, DisplayResolution.MaxSide);
        var height = StepParameters.ReadInt32(parameters, HeightMember, DisplayResolution.MinSide, DisplayResolution.MaxSide);
        DisplayResolution? resolution = (width, height) switch
        {
            ({ } w, { } h) => new DisplayResolution(w, h),
            (null, null) => null,
            (null, _) => throw StepParameters.Required(WidthMember, $"'{HeightMember}' is present"),
            _ => throw StepParameters.Required(HeightMember, $"'{WidthMember}' is present"),
        };
        var target = StepParameters.ReadEnum<DisplayTarget>(parameters, DisplayMember) ?? DisplayTarget.UnderGesture;
        return IsHighest(parameters[RefreshMember])
            ? new DisplayModeStep(resolution, null, target, HighestRefresh: true)
            : new DisplayModeStep(resolution, ReadRefresh(parameters), target);
    }

    public JsonObject Write(IStep step)
    {
        var displayMode = StepParameters.Expect<DisplayModeStep>(step, this);
        var parameters = new JsonObject();
        if (displayMode.Resolution is { } size)
        {
            parameters[WidthMember] = size.Width;
            parameters[HeightMember] = size.Height;
        }

        if (displayMode.HighestRefresh)
        {
            parameters[RefreshMember] = HighestValue;
        }
        else if (displayMode.Refresh is { } rate)
        {
            // A double prints the shortest form (119.88, 120, 23.976); Read rounds back to the same millihertz.
            parameters[RefreshMember] = rate.Millihertz / 1000.0;
        }

        parameters[DisplayMember] = displayMode.Target.ToString();
        return parameters;
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DisplayModeExecutor.Execute(StepParameters.Expect<DisplayModeStep>(step, this), context);
    }

    /// <summary>The string <see cref="HighestValue"/>, in any case.</summary>
    private static bool IsHighest(JsonNode? node)
        => node is JsonValue value && value.TryGetValue(out string? text) && string.Equals(text, HighestValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>A JSON number of hertz, rounded to three decimals; another string, zero, a negative or more than 1000 names the member.</summary>
    private static RefreshRate? ReadRefresh(JsonObject parameters)
    {
        var node = parameters[RefreshMember];
        if (node is null)
        {
            return null;
        }

        if (node is not JsonValue value || !TryGetHertz(value, out var hertz))
        {
            throw new StepFormatException($"'{RefreshMember}' must be a number of hertz or \"{HighestValue}\".");
        }

        var rate = RefreshRate.FromHertz((double)hertz);
        return rate.IsKnown
            ? rate
            : throw new StepFormatException($"'{RefreshMember}' must be above 0 and at most {RefreshRate.MaxHertz}; got {hertz}.");
    }

    private static bool TryGetHertz(JsonValue value, out decimal hertz)
    {
        if (value.TryGetValue(out hertz))
        {
            return true;
        }

        if (value.TryGetValue(out double real) && double.IsFinite(real) && Math.Abs(real) < 1e9)
        {
            hertz = (decimal)real;
            return true;
        }

        if (value.TryGetValue(out long whole))
        {
            hertz = whole;
            return true;
        }

        if (value.TryGetValue(out int small))
        {
            hertz = small;
            return true;
        }

        return false;
    }
}
