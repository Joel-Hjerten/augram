using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.OpenApp;
using Xunit;

namespace Augram.Core.Tests.Platforms;

/// <summary>
/// <see cref="RunningPlatform"/> is process-wide, so these tests run alone (a collection that is never run in parallel):
/// every other test sees it unset.
/// </summary>
[Collection(RunningPlatformTests.Alone)]
public sealed class RunningPlatformTests
{
    public const string Alone = "RunningPlatform, alone";

    private static HostPlatform OperatingSystemAnswer => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;

    [Fact]
    public void Unset_ItIsTheOperatingSystemsAnswer_AndKeyNamesStayWindows()
    {
        Assert.Equal(OperatingSystemAnswer, RunningPlatform.Current);
        Assert.Equal(OperatingSystemAnswer, NullWindowOperations.Instance.Platform);
        Assert.Equal(OperatingSystemAnswer, NullDisplayModes.Instance.Platform);
        Assert.Equal(HostPlatform.Windows, HotkeyText.Names);
    }

    [Fact]
    public void Set_EveryReaderFollows_UntilReset()
    {
        try
        {
            RunningPlatform.Current = HostPlatform.MacOS;
            Assert.Equal(HostPlatform.MacOS, RunningPlatform.Current);
            Assert.Equal(HostPlatform.MacOS, HotkeyText.Names);
            Assert.Equal(HostPlatform.MacOS, NullWindowOperations.Instance.Platform);
            Assert.Equal("Open app (none for macOS)", new OpenAppStep(false, @"C:\Tools\private.exe", string.Empty).Summary);

            RunningPlatform.Current = HostPlatform.Windows;
            Assert.Equal("Win+W", HotkeyText.Format(KeyModifiers.Meta, KeyCode.W));
            Assert.Equal("Open private.exe", new OpenAppStep(false, "private.exe", string.Empty).Summary);
        }
        finally
        {
            RunningPlatform.Reset();
        }

        Assert.Equal(OperatingSystemAnswer, RunningPlatform.Current);
        Assert.Equal(HostPlatform.Windows, HotkeyText.Names);
    }

    [Fact]
    public void UseThisMachine_SetsTheOperatingSystemsAnswer_KeyNamesIncluded()
    {
        try
        {
            RunningPlatform.UseThisMachine();
            Assert.Equal(OperatingSystemAnswer, RunningPlatform.Current);
            Assert.Equal(OperatingSystemAnswer, HotkeyText.Names);
        }
        finally
        {
            RunningPlatform.Reset();
        }
    }

    [Theory]
    [InlineData(HostPlatform.Windows, "Windows", HostPlatform.MacOS)]
    [InlineData(HostPlatform.MacOS, "macOS", HostPlatform.Windows)]
    public void DisplayNameAndOther(HostPlatform platform, string name, HostPlatform other)
    {
        Assert.Equal(name, platform.DisplayName());
        Assert.Equal(other, platform.Other());
    }
}

[CollectionDefinition(RunningPlatformTests.Alone, DisableParallelization = true)]
public sealed class RunningPlatformCollection
{
}
