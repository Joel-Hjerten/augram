using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Launch;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Launch;

/// <summary>What the macOS launcher would start for each kind of target; pure, tested on every OS, nothing is started.</summary>
public sealed class MacStartInfoTests
{
    private const string Home = "/Users/synthetic";

    private static readonly Dictionary<string, string> Executables = new(StringComparer.Ordinal)
    {
        ["say"] = "/usr/bin/say",
        ["/Users/synthetic/bin/sync.sh"] = "/Users/synthetic/bin/sync.sh",
        ["/opt/tools/run tool"] = "/opt/tools/run tool",
    };

    private static string Expand(string text) => text.Replace("%TOOLS%", "/opt/tools", StringComparison.Ordinal);

    private static string? Find(string file) => Executables.GetValueOrDefault(file);

    [Fact]
    public void ALinkGoesToOpenWithoutItsArguments()
    {
        var info = MacStartInfo.For(new ProcessLaunch("https://example.com/a?b=%TOOLS%", "--ignored"), Expand, Home, Find);

        Assert.Equal(MacStartInfo.OpenPath, info.FileName);
        Assert.Equal("https://example.com/a?b=%TOOLS%", info.Arguments);
        Assert.False(info.UseShellExecute);
        Assert.True(info.RedirectStandardError);
    }

    [Fact]
    public void HiddenOpensInTheBackgroundAndHidden()
    {
        Assert.Equal("-g -j x-apple.systempreferences:com.apple.Displays-Settings.extension", MacStartInfo.For(new ProcessLaunch("x-apple.systempreferences:com.apple.Displays-Settings.extension", Hidden: true), Expand, Home, Find).Arguments);
        Assert.Equal("-g -j -a Safari", MacStartInfo.For(new ProcessLaunch("Safari", Hidden: true), Expand, Home, Find).Arguments);
    }

    [Fact]
    public void AnExecutableRunsItselfWithItsArgumentsInTheHomeFolder()
    {
        var info = MacStartInfo.For(new ProcessLaunch("say", "hello world"), Expand, Home, Find);

        Assert.Equal("/usr/bin/say", info.FileName);
        Assert.Equal("hello world", info.Arguments);
        Assert.Equal(Home, info.WorkingDirectory);
        Assert.False(info.UseShellExecute);
        Assert.False(info.RedirectStandardError);
    }

    [Fact]
    public void TildeAndVariablesExpandInTheFileAndStartIn()
    {
        var script = MacStartInfo.For(new ProcessLaunch("~/bin/sync.sh", "--all", "~/work"), Expand, Home, Find);
        Assert.Equal("/Users/synthetic/bin/sync.sh", script.FileName);
        Assert.Equal("/Users/synthetic/work", script.WorkingDirectory);

        var tool = MacStartInfo.For(new ProcessLaunch("%TOOLS%/run tool", string.Empty, "%TOOLS%"), Expand, Home, Find);
        Assert.Equal("/opt/tools/run tool", tool.FileName);
        Assert.Equal("/opt/tools", tool.WorkingDirectory);

        Assert.Equal(Home, MacStartInfo.ExpandPath("~", Expand, Home));
        Assert.Equal("~user/x", MacStartInfo.ExpandPath("~user/x", Expand, Home));
    }

    [Fact]
    public void AnAppByNameOrBundleGoesToOpenAWithItsArguments()
    {
        Assert.Equal("-a Safari", MacStartInfo.For(new ProcessLaunch("Safari"), Expand, Home, Find).Arguments);
        Assert.Equal("-a \"Google Chrome\" --args --incognito https://example.com", MacStartInfo.For(new ProcessLaunch("Google Chrome", "--incognito https://example.com"), Expand, Home, Find).Arguments);
        Assert.Equal("-a \"/Applications/Visual Studio Code.app\"", MacStartInfo.For(new ProcessLaunch("/Applications/Visual Studio Code.app/"), Expand, Home, Find).Arguments);
        Assert.Equal(MacStartInfo.OpenPath, MacStartInfo.For(new ProcessLaunch("Safari"), Expand, Home, Find).FileName);
    }

    [Fact]
    public void ADocumentOrFolderGoesToOpenWithoutArguments()
    {
        Assert.Equal("\"/Users/synthetic/Documents/report 1.pdf\"", MacStartInfo.For(new ProcessLaunch("~/Documents/report 1.pdf", "--ignored"), Expand, Home, Find).Arguments);
        Assert.Equal("/Users/synthetic/Downloads/", MacStartInfo.For(new ProcessLaunch("~/Downloads/"), Expand, Home, Find).Arguments);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "\"\"")]
    [InlineData("two words", "\"two words\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData(@"back\slash", "\"back\\slash\"")]
    [InlineData(@"ends\", "\"ends\\\\\"")]
    [InlineData("a\\\"b", "\"a\\\\\\\"b\"")]
    public void QuotingFollowsTheArgvRules(string argument, string expected)
    {
        Assert.Equal(expected, MacStartInfo.Quote(argument));
    }
}
