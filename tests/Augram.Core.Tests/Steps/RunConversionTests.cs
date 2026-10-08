using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Run;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>F8 for Run: cross-platform links carry over, everything else needs its own version on the other platform.</summary>
public sealed class RunConversionTests
{
    private static readonly RunStepType Type = RunStepType.Instance;

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("HTTP://example.com")]
    [InlineData("mailto:joel@example.com")]
    [InlineData("ftp://example.com/file")]
    public void PortableLinksRunUnchangedBothWays(string file)
    {
        var step = new RunStep(file, Hidden: true);

        Assert.Equal(new StepConversion(StepConversionKind.Unchanged, step, null), Type.Convert(step, HostPlatform.Windows, HostPlatform.MacOS));
        Assert.Equal(new StepConversion(StepConversionKind.Unchanged, step, null), Type.Convert(step, HostPlatform.MacOS, HostPlatform.Windows));
    }

    [Theory]
    [InlineData("explorer", "", "Run explorer needs a macOS version: a program and its path belong to one platform")]
    [InlineData("taskkill.exe", "/f /im yuzu.exe", "Run taskkill.exe needs a macOS version: a program and its path belong to one platform")]
    [InlineData(@"C:\Windows\notepad.exe", "", "Run notepad.exe needs a macOS version: a program and its path belong to one platform")]
    [InlineData("ms-settings:display", "", "Open ms-settings:display needs a macOS version: ms-settings: links open on one platform only")]
    public void WindowsProgramsAndPlatformLinksNeedAMacVersion(string file, string arguments, string reason)
    {
        var conversion = Type.Convert(new RunStep(file, arguments), HostPlatform.Windows, HostPlatform.MacOS);

        Assert.Equal(StepConversionKind.NotConvertible, conversion.Kind);
        Assert.Null(conversion.Step);
        Assert.Equal(reason, conversion.Reason);
    }

    [Fact]
    public void MacProgramsNeedAWindowsVersion()
    {
        var conversion = Type.Convert(new RunStep("/Applications/Safari.app"), HostPlatform.MacOS, HostPlatform.Windows);

        Assert.Equal(StepConversionKind.NotConvertible, conversion.Kind);
        Assert.Equal("Run Safari.app needs a Windows version: a program and its path belong to one platform", conversion.Reason);
    }

    [Fact]
    public void TheReasonNeverShowsTheArguments()
    {
        var conversion = Type.Convert(new RunStep("tool.exe", "--token hunter2"), HostPlatform.Windows, HostPlatform.MacOS);

        Assert.DoesNotContain("hunter2", conversion.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SamePlatformAndUnsetStepsAreUnchanged()
    {
        var program = new RunStep("explorer");

        Assert.Equal(StepConversionKind.Unchanged, Type.Convert(program, HostPlatform.Windows, HostPlatform.Windows).Kind);
        Assert.Equal(StepConversionKind.Unchanged, Type.Convert(program, HostPlatform.MacOS, HostPlatform.MacOS).Kind);
        Assert.Same(RunStep.Unset, Type.Convert(RunStep.Unset, HostPlatform.Windows, HostPlatform.MacOS).Step);
    }
}
