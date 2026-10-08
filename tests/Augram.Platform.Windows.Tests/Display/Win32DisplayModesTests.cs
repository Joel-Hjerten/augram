using Augram.Core.Abstractions;
using Augram.Platform.Windows.Display;
using Xunit;

namespace Augram.Platform.Windows.Tests.Display;

/// <summary>The Windows display adapter's rules against scripted displays: what counts as a display, the rate rule, which setting is sent and how, and which HDR call.</summary>
public sealed class Win32DisplayModesTests
{
    [Fact]
    public void DisplaysAreTheAttachedDevicesWithExactRatesAndEachModeOnce()
    {
        var native = new FakeWin32Displays().WithTv();
        native.DeviceList.Add(new Win32DisplayDevice(@"\\.\DISPLAY5", 0));
        native.DeviceList.Add(new Win32DisplayDevice(@"\\.\DISPLAY6", 0x1 | 0x8));

        var display = Assert.Single(new Win32DisplayModes(native).Displays());

        Assert.Equal(@"\\.\DISPLAY1", display.Id);
        Assert.Equal("SONY TV *30", display.Name);
        Assert.True(display.IsMain);
        Assert.Equal(new DisplayBounds(0, 0, 3840, 2160), display.Bounds);
        Assert.Equal("3840×2160 at 120 Hz", display.Current.ToString());
        Assert.Equal(22, display.Modes.Count);
        Assert.Contains(new VideoMode(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(119.88)), display.Modes);
        Assert.Contains(new VideoMode(new DisplayResolution(3840, 2160), RefreshRate.FromHertz(23.976)), display.Modes);
        Assert.Equal(HdrState.Off, display.Hdr);
    }

    [Fact]
    public void ADisplayWithoutACcdPathIsNamedByItsDeviceAndHasNoHdr()
    {
        var native = new FakeWin32Displays().WithTv();
        native.TargetList.Clear();

        var display = Assert.Single(new Win32DisplayModes(native).Displays());

        Assert.Equal("DISPLAY1", display.Name);
        Assert.Equal(HdrState.Unsupported, display.Hdr);
    }

    [Fact]
    public void ASecondDisplaySitsWhereItsCurrentSettingsPutIt()
    {
        var native = new FakeWin32Displays().WithTv().WithTv(@"\\.\DISPLAY2", primary: false, x: 3840);

        var displays = new Win32DisplayModes(native).Displays();

        Assert.Equal(2, displays.Count);
        Assert.Equal(new DisplayBounds(3840, 0, 3840, 2160), displays[1].Bounds);
        Assert.False(displays[1].IsMain);
    }

    [Theory]
    [InlineData(0x51u, 0x1u, HdrState.Off)]
    [InlineData(0x71u, 0x3u, HdrState.On)]
    [InlineData(0x41u, 0x1u, HdrState.Unsupported)]
    [InlineData(null, 0x3u, HdrState.On)]
    [InlineData(null, 0x1u, HdrState.Off)]
    [InlineData(null, 0x0u, HdrState.Unsupported)]
    [InlineData(null, null, HdrState.Unsupported)]
    public void HdrIsReadFromTheNewerAnswerWhenWindowsHasIt(uint? info2, uint? info, HdrState expected)
    {
        var native = new FakeWin32Displays().WithTv(info2: info2, info: info);

        Assert.Equal(expected, Assert.Single(new Win32DisplayModes(native).Displays()).Hdr);
    }

    [Fact]
    public void SetModeSendsWindowsOwnIntegerTestsFirstThenStores()
    {
        var native = new FakeWin32Displays().WithTv();
        var adapter = new Win32DisplayModes(native);
        var display = adapter.Displays()[0];

        var result = adapter.SetMode(display, new VideoMode(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(119.88)));

        Assert.True(result.Succeeded);
        Assert.Equal([@"test \\.\DISPLAY1 1920x1080@119", @"apply \\.\DISPLAY1 1920x1080@119"], native.Calls);
        Assert.All(native.Applied, setting => Assert.Equal((32, 0, false), (setting.BitsPerPixel, setting.FixedOutput, setting.Interlaced)));
    }

    [Fact]
    public void SetModeKeepsWholeRatesWhole()
    {
        var native = new FakeWin32Displays().WithTv();
        var adapter = new Win32DisplayModes(native);

        adapter.SetMode(adapter.Displays()[0], new VideoMode(new DisplayResolution(3840, 2160), RefreshRate.FromHertz(24)));

        Assert.Equal(@"apply \\.\DISPLAY1 3840x2160@24", native.Calls[^1]);
    }

    [Fact]
    public void ARefusedTestAppliesNothing()
    {
        var native = new FakeWin32Displays().WithTv();
        native.ChangeResults.Enqueue(-2);
        var adapter = new Win32DisplayModes(native);

        var result = adapter.SetMode(adapter.Displays()[0], new VideoMode(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(60)));

        Assert.False(result.Succeeded);
        Assert.Equal("Windows refused 1920×1080 at 60 Hz on SONY TV *30 (DISP_CHANGE_BADMODE: the mode is not supported)", result.Reason);
        Assert.Equal([@"test \\.\DISPLAY1 1920x1080@60"], native.Calls);
    }

    [Fact]
    public void ARestartAnswerIsAFailureThatSaysSo()
    {
        var native = new FakeWin32Displays().WithTv();
        native.ChangeResults.Enqueue(0);
        native.ChangeResults.Enqueue(1);
        var adapter = new Win32DisplayModes(native);

        var result = adapter.SetMode(adapter.Displays()[0], new VideoMode(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(60)));

        Assert.False(result.Succeeded);
        Assert.Contains("DISP_CHANGE_RESTART", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AModeNoLongerListedIsRefusedWithoutAnyCall()
    {
        var native = new FakeWin32Displays().WithTv();
        var adapter = new Win32DisplayModes(native);

        var result = adapter.SetMode(adapter.Displays()[0], new VideoMode(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(144)));

        Assert.Equal("SONY TV *30 no longer offers 1920×1080 at 144 Hz", result.Reason);
        Assert.Empty(native.Calls);
    }

    [Theory]
    [InlineData(0x51u, "hdr-state")]
    [InlineData(null, "advanced-color")]
    public void SetHdrUsesTheCallThatMatchesWhatWasRead(uint? info2, string call)
    {
        var native = new FakeWin32Displays().WithTv(info2: info2);
        var adapter = new Win32DisplayModes(native);

        Assert.True(adapter.SetHdr(adapter.Displays()[0], on: true).Succeeded);
        Assert.Equal([$@"{call} \\.\DISPLAY1 on"], native.Calls);
    }

    [Fact]
    public void SetHdrReportsTheWin32ErrorAndAMissingPath()
    {
        var native = new FakeWin32Displays().WithTv();
        native.HdrResult = 87;
        var adapter = new Win32DisplayModes(native);
        var display = adapter.Displays()[0];

        Assert.Equal("DisplayConfigSetDeviceInfo failed (87)", adapter.SetHdr(display, on: false).Reason);

        native.TargetList.Clear();
        Assert.Equal("SONY TV *30 has no active display path", adapter.SetHdr(display, on: false).Reason);
    }
}
