using Augram.App.Components.Steps;
using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>
/// <see cref="StepFormState{TStep}"/>, the one way an edit leaves a step's form: an edit that changes nothing emits nothing (no
/// undo step), which the Delay and Media key forms now share with the others (plan 0007 S14).
/// </summary>
public sealed class StepFormStateTests
{
    [Fact]
    public void AnEditThatChangesNothingEmitsNothing_AndAnAcceptedOneIsNormalisedFirst()
    {
        var changes = new List<int>();
        var accepted = new List<int>();
        var refreshed = 0;
        var form = new StepFormState<int>(4, changes.Add, value => Math.Max(0, value)) { Accepted = accepted.Add };
        form.OnEdit(() => refreshed++);

        form.Emit(4);
        form.Emit(-1);
        form.Emit(0);

        Assert.Equal([0], changes);
        Assert.Equal([0], accepted);
        Assert.Equal(1, refreshed);
        Assert.Equal(0, form.Current);
    }

    [AvaloniaFact]
    public void TheDelayFormEmitsNoNoOpEdit()
    {
        var changes = new List<IStep>();
        var value = (IValueBinding<double>)Field(new DelayStep(250), changes.Add).Binding!;

        value.Set(250);
        Assert.Empty(changes);

        value.Set(300);
        Assert.Equal(new DelayStep(300), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void TheMediaKeyFormEmitsNoNoOpEdit()
    {
        var changes = new List<IStep>();
        var value = (IValueBinding<MediaKeyKind>)Field(new MediaKeyStep(MediaKeyKind.PlayPause), changes.Add).Binding!;

        value.Set(MediaKeyKind.PlayPause);
        Assert.Empty(changes);

        value.Set(MediaKeyKind.VolumeMute);
        Assert.Equal(new MediaKeyStep(MediaKeyKind.VolumeMute), Assert.Single(changes));
    }

    /// <summary>The one field of a one-field step form, as declared.</summary>
    private static Field Field(IStep step, Action<IStep> changed)
        => ((Augram.App.Components.SectionForm.SectionForm)StepFormRegistry.Default.Build(step, changed)).Screen!.Sections.Single().Fields.Single();
}
