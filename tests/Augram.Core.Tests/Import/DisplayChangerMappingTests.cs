using Augram.Core.Abstractions;
using Augram.Core.Steps.DisplayMode;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// 12noon Display Changer command lines as Display mode steps. The argument strings are the ten shapes Joel's
/// StrokesPlus.net commands use (refresh presets and the 1080p and 2160p resolutions); the path is made up.
/// </summary>
public sealed class DisplayChangerMappingTests
{
    private const string Program = @"C:\Tools\12noon Display Changer\dc64cmd.exe";

    [Theory]
    [InlineData("-refresh=23", 23.976)]
    [InlineData("-refresh=24", 24.0)]
    [InlineData("-refresh=25", 25.0)]
    [InlineData("-refresh=30", 30.0)]
    [InlineData("-refresh=50", 50.0)]
    [InlineData("-refresh=60", 60.0)]
    [InlineData("-refresh=100", 100.0)]
    [InlineData("-refresh=120", 120.0)]
    [InlineData("-refresh=119", 119.88)]
    [InlineData("-refresh=59", 59.94)]
    public void ARefreshPresetIsARateOnlyStepThroughWindowsWholeHertzRule(string arguments, double hertz)
    {
        var step = DisplayChangerMapping.FromInvocation(Program, arguments);

        Assert.Equal(new DisplayModeStep(Refresh: RefreshRate.FromHertz(hertz)), step);
        Assert.Equal(DisplayTarget.UnderGesture, step!.Target);
    }

    [Theory]
    [InlineData("-refresh=max", null, "Display refresh highest available")]
    [InlineData("-width=1920 -height=1080 -refresh=MAX", 1920, "Display 1920×1080 at the highest refresh")]
    public void RefreshMaxIsTheHighestAvailableRate(string arguments, int? width, string summary)
    {
        var step = DisplayChangerMapping.FromInvocation(Program, arguments);

        Assert.Equal(new DisplayModeStep(width is { } w ? new DisplayResolution(w, 1080) : null, HighestRefresh: true), step);
        Assert.Equal(summary, step!.Summary);
    }

    [Theory]
    [InlineData("-width=1920 -height=1080", 1920, 1080, "Display 1920×1080")]
    [InlineData("-width=3840 -height=2160", 3840, 2160, "Display 3840×2160")]
    [InlineData("-height=2160   -width=3840", 3840, 2160, "Display 3840×2160")]
    public void AResolutionPresetKeepsTheRateAuto(string arguments, int width, int height, string summary)
    {
        var step = DisplayChangerMapping.FromInvocation(Program, arguments);

        Assert.Equal(new DisplayModeStep(new DisplayResolution(width, height)), step);
        Assert.Equal(summary, step!.Summary);
    }

    [Fact]
    public void BothTogetherAndQuietAreOneStep()
    {
        Assert.Equal(
            new DisplayModeStep(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(23.976)),
            DisplayChangerMapping.FromInvocation(Program, "-quiet -width=1920 -height=1080 -refresh=23"));
    }

    [Theory]
    [InlineData("dccmd.exe")]
    [InlineData(@"""C:\Program Files\Display Changer\DCCMD.EXE""")]
    [InlineData("/Users/joel/bin/dc64cmd.exe")]
    public void BothCommandLineProgramsAreRecognised(string fileName)
    {
        Assert.True(DisplayChangerMapping.IsDisplayChanger(fileName));
        Assert.NotNull(DisplayChangerMapping.FromInvocation(fileName, "/refresh=60"));
    }

    [Theory]
    [InlineData(@"C:\Tools\dc64.exe")]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData(@"C:\Tools\notdc64cmd.exe")]
    [InlineData("")]
    public void OtherProgramsAreNotDisplayChanger(string fileName)
    {
        Assert.False(DisplayChangerMapping.IsDisplayChanger(fileName));
        Assert.Null(DisplayChangerMapping.FromInvocation(fileName, "-refresh=60"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-quiet")]
    [InlineData("-width=1920")]
    [InlineData("-height=1080 -refresh=60")]
    [InlineData("-width=max -height=max")]
    [InlineData("-refresh=0")]
    [InlineData("-refresh=60 -refresh=24")]
    [InlineData("-refresh=59.94")]
    [InlineData("-width=0 -height=1080")]
    [InlineData("-refresh=60 -depth=32")]
    [InlineData("-refresh=60 -force")]
    [InlineData("-refresh=60 -test")]
    [InlineData("-listmodes")]
    [InlineData(@"-monitor=""\\.\DISPLAY2"" -refresh=60")]
    [InlineData(@"-monitor=""Dell 2007FP (Digital)"" -width=1024 -height=768")]
    [InlineData(@"-width=640 -height=480 ""C:\Program Files\Game\game.exe""")]
    public void AnythingTheStepCannotSayStaysARunStep(string arguments)
    {
        Assert.Null(DisplayChangerMapping.FromInvocation(Program, arguments));
    }
}
