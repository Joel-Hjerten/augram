using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class AppStateTests
{
    [Fact]
    public void StartsEnabledAndToggleFlipsIt()
    {
        var state = new AppState();
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        state.Toggle();
        Assert.False(state.Enabled);

        state.Toggle();
        Assert.True(state.Enabled);
        Assert.Equal([nameof(AppState.Enabled), nameof(AppState.Enabled)], changes);
    }

    [Fact]
    public void SettingTheSameValueRaisesNothing()
    {
        var state = new AppState();
        var raised = 0;
        state.PropertyChanged += (_, _) => raised++;

        state.Enabled = true;
        state.StartAtLogin = false;

        Assert.Equal(0, raised);
    }
}
