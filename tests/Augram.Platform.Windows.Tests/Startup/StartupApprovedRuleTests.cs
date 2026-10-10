using Augram.Platform.Windows.Startup;
using Xunit;

namespace Augram.Platform.Windows.Tests.Startup;

/// <summary>Task Manager's Startup apps switch: an even first byte (or no value) is on, an odd one is off.</summary>
public sealed class StartupApprovedRuleTests
{
    [Theory]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[0])]
    [InlineData(null)]
    public void EvenOrAbsentIsEnabled(byte[]? value) => Assert.False(StartupApprovedRule.IsDisabled(value));

    [Theory]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 0x5A, 0x1F, 0x3C, 0x22, 0x9E, 0x40, 0xDB, 0x01 })]
    [InlineData(new byte[] { 0x07, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x01 })]
    public void OddIsDisabled(byte[] value) => Assert.True(StartupApprovedRule.IsDisabled(value));
}
