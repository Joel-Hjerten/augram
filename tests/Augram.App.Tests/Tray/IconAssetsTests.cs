using System.Buffers.Binary;
using Augram.App.Tests.Architecture;
using Augram.App.Tray;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Tray;

/// <summary>
/// Pins the icons tools/IconTool generates from design/app-icon (the same numbers as its IconSizes): a missing or
/// wrong-sized icon fails here instead of showing as a blank tray or a stretched taskbar icon. Regenerate with
/// <c>dotnet run --project tools/IconTool</c>.
/// </summary>
public sealed class IconAssetsTests
{
    private static string App => Path.Combine(RepositoryPaths.Root, "src", "Augram.App");

    [Theory]
    [InlineData("Assets/Icons/augram-256.png", 256)]
    [InlineData("Assets/tray-enabled.png", 32)]
    [InlineData("Assets/tray-disabled.png", 32)]
    [InlineData("Assets/tray-mac-enabled.png", 36)]
    [InlineData("Assets/tray-mac-disabled.png", 36)]
    [InlineData("Assets/tray-mac-colour-enabled.png", 36)]
    [InlineData("Assets/tray-mac-colour-disabled.png", 36)]
    public void RuntimeIconsHaveTheirSize(string file, int size)
        => Assert.Equal((size, size), PngSize(File.ReadAllBytes(Path.Combine(App, file))));

    [Fact]
    public void TheExeIconHoldsEverySizeAsPng()
        => Assert.Equal([16, 20, 24, 32, 40, 48, 64, 128, 256], IcoSizes(File.ReadAllBytes(Path.Combine(App, "Icons", "augram.ico"))));

    /// <summary>The sizes in an .ico's directory, checking each entry is a PNG of that size.</summary>
    private static List<int> IcoSizes(byte[] bytes)
    {
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)));
        var count = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4));
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var entry = 6 + (16 * i);
            var side = bytes[entry] == 0 ? 256 : bytes[entry];
            var offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entry + 12));
            Assert.Equal((side, side), PngSize(bytes.AsSpan(offset).ToArray()));
            sizes.Add(side);
        }

        return sizes;
    }

    [Fact]
    public void TheMacIconSkipsTheLegacySmallSlots()
    {
        var bytes = File.ReadAllBytes(Path.Combine(App, "Icons", "augram.icns"));
        Assert.Equal("icns", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(4)));
        var types = new List<string>();
        for (var offset = 8; offset < bytes.Length;)
        {
            types.Add(System.Text.Encoding.ASCII.GetString(bytes, offset, 4));
            offset += BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 4));
        }

        Assert.Equal(["ic07", "ic08", "ic09", "ic10", "ic11", "ic12", "ic13", "ic14"], types);
        Assert.DoesNotContain("icp4", types);
    }

    [Fact]
    public void TheMasterArtIsInTheDesignFolder()
        => Assert.True(File.Exists(Path.Combine(RepositoryPaths.Root, "design", "app-icon", "exports", "app-icon.png")));

    [Theory]
    [InlineData("tray-enabled.ico")]
    [InlineData("tray-disabled.ico")]
    public void TheWindowsTrayIconsHoldOneImagePerScaling(string file)
        => Assert.Equal([16, 20, 24, 32, 40, 48], IcoSizes(File.ReadAllBytes(Path.Combine(App, "Assets", file))));

    [AvaloniaFact]
    public void TheTrayLoadsEverySetAndMarksOnlyTheMacOneAsATemplate()
    {
        var windows = TrayIconSet.Load(TrayIconKind.WindowsIco);
        var png = TrayIconSet.Load(TrayIconKind.Png);
        var mac = TrayIconSet.Load(TrayIconKind.MacTemplate);
        var macColour = TrayIconSet.Load(TrayIconKind.MacColour);

        Assert.False(windows.IsTemplate);
        Assert.False(png.IsTemplate);
        Assert.True(mac.IsTemplate);
        Assert.False(macColour.IsTemplate);
        Assert.NotSame(windows.Enabled, windows.Disabled);
        Assert.NotSame(mac.Enabled, mac.Disabled);
        Assert.NotSame(macColour.Enabled, macColour.Disabled);
    }

    private static (int Width, int Height) PngSize(byte[] png)
    {
        Assert.Equal("PNG", System.Text.Encoding.ASCII.GetString(png, 1, 3));
        return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
    }
}
