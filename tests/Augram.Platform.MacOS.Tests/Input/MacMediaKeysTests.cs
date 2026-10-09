using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Input;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Input;

/// <summary>The media keys as macOS takes them: <c>NX_KEYTYPE_*</c> numbers and the system-defined event's data1 and flags.</summary>
public sealed class MacMediaKeysTests
{
    [Theory]
    [InlineData(KeyCode.VolumeUp, 0)]
    [InlineData(KeyCode.VolumeDown, 1)]
    [InlineData(KeyCode.VolumeMute, 7)]
    [InlineData(KeyCode.MediaPlay, 16)]
    [InlineData(KeyCode.MediaNext, 17)]
    [InlineData(KeyCode.MediaPrevious, 18)]
    public void EveryMacMediaKeyMaps(KeyCode key, int keyType)
    {
        Assert.True(MacMediaKeys.TryMap(key, out var mapped));
        Assert.Equal(keyType, mapped);
    }

    [Theory]
    [InlineData(KeyCode.MediaStop)]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.F1)]
    public void OtherKeysAndStopGoToTheToolkit(KeyCode key) => Assert.False(MacMediaKeys.TryMap(key, out _));

    [Fact]
    public void Data1CarriesTheKeyTypeAndTheState()
    {
        Assert.Equal((nint)0x10A00, MacMediaKeys.Data1(1, pressed: true));
        Assert.Equal((nint)0x10B00, MacMediaKeys.Data1(1, pressed: false));
        Assert.Equal((nint)0x100A00, MacMediaKeys.Data1(16, pressed: true));
    }

    [Fact]
    public void FlagsCarryTheState()
    {
        Assert.Equal((nuint)0xA00, MacMediaKeys.ModifierFlags(pressed: true));
        Assert.Equal((nuint)0xB00, MacMediaKeys.ModifierFlags(pressed: false));
    }
}
