using Augram.Core.Steps;
using Avalonia.Controls;

namespace Augram.App.Components.Steps;

/// <summary>
/// The parameter form of one step type (ADR-0002 §4: it lives beside nothing but its own folder,
/// <c>Components/Steps/&lt;Name&gt;/</c>). <see cref="Build"/> returns a control showing
/// <c>current</c>'s parameters and calls <c>changed</c> with a new step record on every edit; the host
/// commits it (one undo step per call). Found by assembly scan in <see cref="StepFormRegistry"/>, so a
/// new type registers its form by existing. Implementations need a parameterless constructor.
/// </summary>
public interface IStepForm
{
    /// <summary>Matches <see cref="IStepType.Key"/>.</summary>
    string TypeKey { get; }

    Control Build(IStep current, Action<IStep> changed);
}
