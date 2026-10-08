using System.Reflection;
using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>Version, commit and channel read from the assembly attributes the build writes (Directory.Build.props, Augram.App.csproj).</summary>
public sealed class AppInfoTests
{
    private const string FullCommit = "3f1c2ab9d0e1f2a3b4c5d6e7f8091a2b3c4d5e6f";

    [Fact]
    public void ADevBuild_SaysDevEverywhere()
    {
        var app = AppInfo.Parse("0.2.0+" + FullCommit, "Dev");

        Assert.Equal("0.2.0", app.Version);
        Assert.Equal("3f1c2ab", app.Commit);
        Assert.Equal("0.2.0+3f1c2ab", app.InformationalVersion);
        Assert.Equal(AppChannel.Dev, app.Channel);
        Assert.True(app.IsDev);
        Assert.Equal("Augram (Dev)", app.DisplayName);
        Assert.Equal("Augram (Dev) 0.2.0", app.Describe());
        Assert.Equal("Dev (development build)", app.ChannelText);
    }

    [Fact]
    public void TheReleaseBuild_IsPlainAugram()
    {
        var app = AppInfo.Parse("0.2.0+" + FullCommit, "Release");

        Assert.Equal(AppChannel.Release, app.Channel);
        Assert.False(app.IsDev);
        Assert.Equal("Augram", app.DisplayName);
        Assert.Equal("Augram 0.2.0 (installed)", app.Describe());
        Assert.Equal("Release (installed)", app.ChannelText);
    }

    [Theory]
    [InlineData(null, AppChannel.Dev)]
    [InlineData("", AppChannel.Dev)]
    [InlineData("Beta", AppChannel.Dev)]
    [InlineData("release", AppChannel.Release)]
    [InlineData(" Release ", AppChannel.Release)]
    public void OnlyReleaseIsRelease_AnythingElseIsTheSafeDev(string? channel, AppChannel expected)
    {
        Assert.Equal(expected, AppInfo.Parse("0.2.0", channel).Channel);
    }

    [Theory]
    [InlineData("0.2.0", "0.2.0", null)]
    [InlineData("0.2.0+abc", "0.2.0", "abc")]
    [InlineData("1.0.0+build.5." + FullCommit, "1.0.0", "3f1c2ab")]
    [InlineData("0.2.0+", "0.2.0", null)]
    public void TheCommitIsTheShortHashAfterThePlus(string informational, string version, string? commit)
    {
        var app = AppInfo.Parse(informational, "Dev");

        Assert.Equal(version, app.Version);
        Assert.Equal(commit, app.Commit);
    }

    [Fact]
    public void TheAppAssemblyCarriesTheVersionAndTheChannel()
    {
        var assembly = typeof(AppInfo).Assembly;
        var channel = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(a => a.Key == AppInfo.ChannelMetadataKey);
        Assert.NotNull(channel);

        var app = AppInfo.FromAssembly(assembly);

        Assert.Equal(AppInfo.ParseChannel(channel.Value), app.Channel);
        Assert.Equal(assembly.GetName().Version!.ToString(3), app.Version);
        Assert.Same(AppInfo.Current, AppInfo.Current);
        Assert.Equal(app, AppInfo.Current);
        if (app.Commit is not null)
        {
            Assert.Matches("^[0-9a-f]{7}$", app.Commit);
        }
    }
}
