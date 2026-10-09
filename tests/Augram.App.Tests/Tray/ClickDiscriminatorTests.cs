using Augram.App.Tray;
using Xunit;

namespace Augram.App.Tests.Tray;

public sealed class ClickDiscriminatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LoneClickBecomesSingleAfterTheWindow()
    {
        var clicks = new ClickDiscriminator(TimeSpan.FromMilliseconds(250));

        Assert.Equal(ClickKind.Pending, clicks.Click(T0));
        Assert.Equal(ClickKind.None, clicks.Flush(T0.AddMilliseconds(100)));
        Assert.Equal(ClickKind.Single, clicks.Flush(T0.AddMilliseconds(300)));
        Assert.Equal(ClickKind.None, clicks.Flush(T0.AddMilliseconds(600)));
    }

    [Fact]
    public void TwoClicksInsideTheWindowBecomeDouble()
    {
        var clicks = new ClickDiscriminator(TimeSpan.FromMilliseconds(250));

        Assert.Equal(ClickKind.Pending, clicks.Click(T0));
        Assert.Equal(ClickKind.Double, clicks.Click(T0.AddMilliseconds(200)));
        Assert.Equal(ClickKind.None, clicks.Flush(T0.AddMilliseconds(600)));
    }

    [Fact]
    public void TwoClicksOutsideTheWindowAreTwoSingles()
    {
        var clicks = new ClickDiscriminator(TimeSpan.FromMilliseconds(250));

        clicks.Click(T0);
        Assert.Equal(ClickKind.Single, clicks.Flush(T0.AddMilliseconds(300)));
        Assert.Equal(ClickKind.Pending, clicks.Click(T0.AddMilliseconds(400)));
        Assert.Equal(ClickKind.Single, clicks.Flush(T0.AddMilliseconds(700)));
    }

    /// <summary>Windows can report a double click as up, double, up: the third is swallowed, so nothing toggles after the window opens.</summary>
    [Fact]
    public void AClickRightAfterADouble_IsSwallowed()
    {
        var clicks = new ClickDiscriminator();
        var start = DateTimeOffset.UnixEpoch;

        Assert.Equal(ClickKind.Pending, clicks.Click(start));
        Assert.Equal(ClickKind.Double, clicks.Click(start.AddMilliseconds(120)));
        Assert.Equal(ClickKind.None, clicks.Click(start.AddMilliseconds(200)));
        Assert.Equal(ClickKind.None, clicks.Flush(start.AddMilliseconds(600)));

        Assert.Equal(ClickKind.Pending, clicks.Click(start.AddMilliseconds(700)));
    }

    [Fact]
    public void TheWindowIs300Ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(300), ClickDiscriminator.DefaultWindow);
    }

    [Fact]
    public void AFlushRightAtTheWindow_IsTheSingle()
    {
        var clicks = new ClickDiscriminator();
        var start = DateTimeOffset.UnixEpoch;

        clicks.Click(start);

        Assert.Equal(ClickKind.Single, clicks.Flush(start + ClickDiscriminator.DefaultWindow));
    }
}
