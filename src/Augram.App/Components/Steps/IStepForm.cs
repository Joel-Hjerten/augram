using Augram.Core.Steps;
using Avalonia.Controls;

namespace Augram.App.Components.Steps;

/// <summary>
/// The parameter form of one step type (ADR-0002 §4: it lives beside nothing but its own folder,
/// <c>Components/Steps/&lt;Name&gt;/</c>). <see cref="Build(IStep, Action{IStep})"/> returns a control showing
/// <c>current</c>'s parameters and calls <c>changed</c> with a new step record on every edit; the host
/// commits it (one undo step per call). A form that adapts to the command it belongs to (the Remap form on a button trigger,
/// plan 0005) also implements <see cref="Build(IStep, Action{IStep}, StepFormContext)"/>; the rest ignore the context. Found by
/// assembly scan in <see cref="StepFormRegistry"/>, so a new type registers its form by existing. Implementations need a
/// parameterless constructor.
/// </summary>
public interface IStepForm
{
    /// <summary>Matches <see cref="IStepType.Key"/>.</summary>
    string TypeKey { get; }

    Control Build(IStep current, Action<IStep> changed);

    /// <summary>The form for a step of a command in <paramref name="context"/>; by default the same as without one.</summary>
    Control Build(IStep current, Action<IStep> changed, StepFormContext context) => Build(current, changed);
}
