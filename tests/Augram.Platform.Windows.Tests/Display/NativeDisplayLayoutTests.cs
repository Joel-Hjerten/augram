using System.Runtime.CompilerServices;
using Augram.Platform.Windows.Interop;
using Xunit;

namespace Augram.Platform.Windows.Tests.Display;

/// <summary>The display interop structs have the sizes Windows checks in their size members; no call is made.</summary>
public sealed class NativeDisplayLayoutTests
{
    [Fact]
    public void StructSizesMatchTheWin32Headers()
    {
        Assert.Equal(220, Unsafe.SizeOf<NativeMethods.DevMode>());
        Assert.Equal(840, Unsafe.SizeOf<NativeMethods.DisplayDevice>());
        Assert.Equal(20, Unsafe.SizeOf<NativeMethods.DisplayConfigDeviceInfoHeader>());
        Assert.Equal(84, Unsafe.SizeOf<NativeMethods.DisplayConfigSourceDeviceName>());
        Assert.Equal(420, Unsafe.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>());
        Assert.Equal(36, Unsafe.SizeOf<NativeMethods.DisplayConfigAdvancedColorInfo>());
        Assert.Equal(24, Unsafe.SizeOf<NativeMethods.DisplayConfigSetState>());
        Assert.Equal(72, Unsafe.SizeOf<NativeMethods.DisplayConfigPathInfo>());
        Assert.Equal(64, Unsafe.SizeOf<NativeMethods.DisplayConfigModeInfo>());
    }
}
