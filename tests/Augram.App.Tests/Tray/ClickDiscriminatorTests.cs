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
}
