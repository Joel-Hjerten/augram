using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.TypeText;

/// <summary>
/// The "Type text" step type: key <c>typeText</c>, category Text, platform-neutral (the same characters on every
/// platform). Parameters: <c>{ "text": "fov 67.5", "method": "Unicode" }</c>, the text as a JSON string (line breaks
/// included) and the method as a <see cref="TypeTextMethod"/> name, matched ignoring case; both default when absent
/// (empty text, Unicode). The default instance is <see cref="TypeTextStep.Empty"/>, which runs as Skipped "no text set".
/// </summary>
public sealed class TypeTextStepType : IStepType
{
    public const string TextMember = "text";

    public const string MethodMember = "method";

    private TypeTextStepType()
    {
    }

    public static TypeTextStepType Instance { get; } = new();

    public string Key => "typeText";

    public string DisplayName => "Type text";

    public StepCategory Category => StepCategory.Text;

    public bool IsPlatformNeutral => true;

    /// <summary>The text may be a password: an export says it travels as written (plan 0003).</summary>
    public bool MayHoldPrivateText => true;

    /// <summary>Text is text on every platform, and the US-layout keys of <see cref="TypeTextMethod.Keys"/> are the same keys on both.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => TypeTextStep.Empty;

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var text = StepParameters.ReadString(parameters, TextMember) ?? string.Empty;
        var method = StepParameters.ReadEnum<TypeTextMethod>(parameters, MethodMember) ?? TypeTextMethod.Unicode;
        return new TypeTextStep(text, method);
    }

    public JsonObject Write(IStep step)
    {
        var typeText = StepParameters.Expect<TypeTextStep>(step, this);
        return new JsonObject { [TextMember] = typeText.Text, [MethodMember] = typeText.Method.ToString() };
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TypeTextExecutor.Execute(StepParameters.Expect<TypeTextStep>(step, this), context);
    }
}
