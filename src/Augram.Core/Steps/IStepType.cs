using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps;

/// <summary>
/// Everything the rest of Augram needs to know about one kind of step, declared from inside that
/// step's folder (ADR-0002 §4, N3): identity and picker metadata, the default instance, how its
/// parameters read from and write to the config file, whether it is the same on every platform, and
/// how it runs. Nowhere else is there a <c>switch</c> over step kinds. The App's parameter form for
/// the type lives beside it under <c>App/Components/Steps/&lt;Name&gt;/</c>.
/// </summary>
public interface IStepType
{
    /// <summary>Stable key written to the config file ("windowOp", "hotkey"); never renamed once shipped.</summary>
    string Key { get; }

    /// <summary>What the step picker shows: "Window", "Hotkey".</summary>
    string DisplayName { get; }

    StepCategory Category { get; }

    /// <summary>
    /// True when the step means the same thing on every platform and needs no conversion or override
    /// (window operations, delays, media keys). False when the parameters are platform-bound (a hotkey's
    /// modifiers, a command line) and F8's conversion or per-platform override applies.
    /// </summary>
    bool IsPlatformNeutral { get; }

    /// <summary>
    /// True for a type the step picker offers only for a command under a hold remap (plan 0002: the Remap step), false (the
    /// default) for one it offers everywhere. The picker reads this; it never names a type.
    /// </summary>
    bool HoldRemapsOnly => false;

    IStep CreateDefault();

    /// <summary>Reads the step's parameters as written by <see cref="Write"/>; missing members take their defaults, unknown ones are ignored.</summary>
    /// <exception cref="StepFormatException">A present member has a value the type cannot use.</exception>
    IStep Read(JsonObject parameters);

    /// <summary>Writes the step's parameters only (the envelope with the type key and platform is the caller's).</summary>
    JsonObject Write(IStep step);

    /// <summary>
    /// What <paramref name="step"/>, authored on <paramref name="from"/>, does on <paramref name="to"/> (F8, both ways, best
    /// effort): <see cref="StepConversion.Same"/> for a platform-neutral type, a converted step, or <see cref="StepConversion.None"/>
    /// with the reason when there is no sensible guess. Pure; called for display and before every run, never stored.
    /// </summary>
    StepConversion Convert(IStep step, HostPlatform from, HostPlatform to);

    /// <summary>Runs the step on the executor thread. Must not throw for an expected failure; return it.</summary>
    StepResult Execute(IStep step, StepExecutionContext context);
}
