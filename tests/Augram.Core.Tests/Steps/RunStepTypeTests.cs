using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Run;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class RunStepTypeTests
{
    private static readonly RunStepType Type = RunStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("run", Type.Key);
        Assert.Equal("Run", Type.DisplayName);
        Assert.Equal(StepCategory.Run, Type.Category);
        Assert.False(Type.IsPlatformNeutral);

        var step = Assert.IsType<RunStep>(Type.CreateDefault());
        Assert.Same(RunStep.Unset, step);
        Assert.False(step.IsSet);
        Assert.Equal("Run (no program set)", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData("explorer", "", false, false, "Run explorer")]
    [InlineData("taskkill.exe", "/f /im yuzu.exe", true, true, "Run taskkill.exe /f /im yuzu.exe (as admin, hidden)")]
    [InlineData("taskkill.exe", "/f /im yuzu.exe", true, false, "Run taskkill.exe /f /im yuzu.exe (as admin)")]
    [InlineData("taskkill.exe", "", false, true, "Run taskkill.exe (hidden)")]
    [InlineData("ms-settings:display", "", false, false, "Open ms-settings:display")]
    [InlineData("https://example.com/a b", "", false, false, "Open https://example.com/a b")]
    [InlineData(@"J:\Synthetic Drive\Tools\dc64cmd.exe", "-refresh=120", false, false, "Run dc64cmd.exe -refresh=120")]
    [InlineData(@"%WINDIR%\notepad.exe", "", false, false, "Run notepad.exe")]
    [InlineData("/Applications/Safari.app/", "", false, false, "Run Safari.app")]
    [InlineData("~/bin/sync.sh", "  --all  ", false, false, "Run sync.sh --all")]
    [InlineData(@"C:\", "", false, false, "Run C:")]
    [InlineData("  explorer  ", "", false, false, "Run explorer")]
    [InlineData("   ", "/x", true, true, "Run (no program set)")]
    public void SummaryNamesWhatRunsAndHow(string file, string arguments, bool elevated, bool hidden, string expected)
    {
        Assert.Equal(expected, new RunStep(file, arguments, Elevated: elevated, Hidden: hidden).Summary);
    }

    [Theory]
    [InlineData("taskkill.exe", "/f /im synthetic.exe", true, true, "Run taskkill.exe … (as admin, hidden)")]
    [InlineData("tool.exe", "--password=secret", false, false, "Run tool.exe …")]
    [InlineData("explorer", "", false, false, "Run explorer")]
    public void LogSummaryNamesTheProgramButNeverItsArguments(string file, string arguments, bool elevated, bool hidden, string expected)
    {
        IStep step = new RunStep(file, arguments, Elevated: elevated, Hidden: hidden);

        Assert.Equal(expected, step.LogSummary);
    }

    [Fact]
    public void TargetLeavesTheArgumentsOut()
    {
        Assert.Equal("Run taskkill.exe", new RunStep("taskkill.exe", "/f /im yuzu.exe", Elevated: true).Target);
        Assert.Equal("Open mailto:joel@example.com", new RunStep("mailto:joel@example.com").Target);
        Assert.True(new RunStep("mailto:joel@example.com").IsUri);
        Assert.False(new RunStep(@"C:\Windows\notepad.exe").IsUri);
    }

    [Fact]
    public void ToLaunchTrimsAndCarriesEveryParameter()
    {
        var step = new RunStep(" taskkill.exe ", " /f /im yuzu.exe ", @" C:\Temp ", Elevated: true, Hidden: true);

        Assert.Equal(new ProcessLaunch("taskkill.exe", "/f /im yuzu.exe", @"C:\Temp", Elevated: true, Hidden: true), step.ToLaunch());
    }

    [Fact]
    public void EveryParameterRoundTripsByteStable()
    {
        var steps = new[]
        {
            new RunStep("taskkill.exe", "/f /im yuzu.exe", @"C:\Temp", Elevated: true, Hidden: true),
            new RunStep("explorer"),
            new RunStep("ms-settings:display", Hidden: true),
            new RunStep(@"""quoted"" \ path", "a \"b\"", "~/x"),
            RunStep.Unset,
        };

        foreach (var step in steps)
        {
            var written = Type.Write(step);
            var reread = Type.Read(written);

            Assert.Equal(step, reread);
            Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
        }

        Assert.Equal(
            """{"file":"taskkill.exe","arguments":"/f /im yuzu.exe","workingDirectory":"","elevated":true,"hidden":true}""",
            Type.Write(new RunStep("taskkill.exe", "/f /im yuzu.exe", Elevated: true, Hidden: true)).ToJsonString());
    }

    [Fact]
    public void ReadTakesDefaultsForMissingMembersAndIgnoresUnknownOnes()
    {
        Assert.Equal(RunStep.Unset, Type.Read(StepJson.Object("{}")));
        Assert.Equal(new RunStep("explorer"), Type.Read(StepJson.Object("""{ "file": "explorer", "arguments": null, "colour": "red" }""")));
        Assert.Equal(new RunStep("x", Elevated: true), Type.Read(StepJson.Object("""{ "file": "x", "elevated": true, "hidden": false }""")));
    }

    [Theory]
    [InlineData("""{ "file": 3 }""", "'file' must be a string.")]
    [InlineData("""{ "file": ["explorer"] }""", "'file' must be a string.")]
    [InlineData("""{ "arguments": false }""", "'arguments' must be a string.")]
    [InlineData("""{ "workingDirectory": {} }""", "'workingDirectory' must be a string.")]
    [InlineData("""{ "elevated": "true" }""", "'elevated' must be true or false.")]
    [InlineData("""{ "hidden": 1 }""", "'hidden' must be true or false.")]
    public void BadInputNamesTheMember(string json, string expected)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public void WriteExecuteAndConvertRefuseAForeignStep()
    {
        var foreign = new DelayStep(1);

        Assert.Throws<ArgumentException>(() => Type.Write(foreign));
        Assert.Throws<ArgumentException>(() => Type.Execute(foreign, StepContexts.Create()));
        Assert.Throws<ArgumentException>(() => Type.Convert(foreign, HostPlatform.Windows, HostPlatform.MacOS));
    }

    [Theory]
    [InlineData("https://example.com", "https")]
    [InlineData("ms-settings:display", "ms-settings")]
    [InlineData("  mailto:joel@example.com", "mailto")]
    [InlineData("x-apple.systempreferences:com.apple.preference.displays", "x-apple.systempreferences")]
    [InlineData("shell:startup", "shell")]
    [InlineData(@"C:\Windows\notepad.exe", null)]
    [InlineData("C:/Windows/notepad.exe", null)]
    [InlineData("explorer", null)]
    [InlineData(@"\\server\share\x.exe", null)]
    [InlineData("~/bin/x", null)]
    [InlineData("1password:open", null)]
    [InlineData("my file:1", null)]
    [InlineData("", null)]
    public void SchemeOfTellsALinkFromAPath(string target, string? expected)
    {
        Assert.Equal(expected, ProcessLaunch.SchemeOf(target));
        Assert.Equal(expected, new ProcessLaunch(target).UriScheme);
    }
}
