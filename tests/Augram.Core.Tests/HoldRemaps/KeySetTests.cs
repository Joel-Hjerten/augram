using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary><see cref="KeySet.Without"/> and <see cref="KeySet.IsEmpty"/>, which the Engine's hold remap shadow keeps its keys with.</summary>
public sealed class KeySetTests
{
    [Theory]
    [InlineData(KeyCode.None)]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.Digit0)]
    [InlineData(KeyCode.Escape)]
    [InlineData(KeyCode.LeftShift)]
    [InlineData(KeyCode.NumPad0)]
    [InlineData(KeyCode.MediaPlay)]
    [InlineData(KeyCode.AppCalculator)]
    public void WithoutTakesOutExactlyThatKey(KeyCode key)
    {
        var all = Enum.GetValues<KeyCode>().Aggregate(default(KeySet), (set, each) => set.With(each));

        var rest = all.Without(key);

        Assert.False(rest.Contains(key));
        Assert.All(Enum.GetValues<KeyCode>().Where(other => other != key), other => Assert.True(rest.Contains(other)));
        Assert.True(default(KeySet).With(key).Without(key).IsEmpty);
        Assert.Equal(rest, rest.Without(key));
    }

    [Fact]
    public void ValuesOutsideTheRangeAreNeverInTheSet()
    {
        var set = default(KeySet).With(KeyCode.A);

        Assert.Equal(set, set.Without((KeyCode)256));
        Assert.Equal(set, set.Without((KeyCode)(-1)));
        Assert.True(default(KeySet).With((KeyCode)300).IsEmpty);
    }

    [Fact]
    public void EveryKeyTheHookCanReportFitsTheSet()
    {
        Assert.All(Enum.GetValues<KeyCode>(), key => Assert.InRange((int)key, 0, 255));
    }
}
