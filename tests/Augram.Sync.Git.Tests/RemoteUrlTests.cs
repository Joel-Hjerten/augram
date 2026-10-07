using Xunit;

namespace Augram.Sync.Git.Tests;

public sealed class RemoteUrlTests
{
    [Theory]
    [InlineData("https://github.com/joel/augram-settings.git", "https://GitHub.com/joel/augram-settings")]
    [InlineData("https://github.com/joel/augram-settings", "https://github.com/joel/augram-settings/")]
    [InlineData("https://github.com/joel/augram-settings.git/", "HTTPS://github.com/joel/augram-settings")]
    [InlineData("https://joel@github.com/joel/augram-settings.git", "https://github.com/joel/augram-settings")]
    [InlineData("git@github.com:joel/augram-settings.git", "git@GITHUB.com:joel/augram-settings")]
    [InlineData("/srv/git/settings.git", "/srv/git/settings/")]
    public void SpellingsOfTheSameRepositoryMatch(string first, string second)
    {
        Assert.True(RemoteUrl.SameRepository(first, second));
    }

    [Theory]
    [InlineData("https://github.com/joel/augram-settings", "https://github.com/joel/other-settings")]
    [InlineData("https://github.com/joel/augram-settings", "https://gitlab.com/joel/augram-settings")]
    [InlineData("https://github.com/joel/augram-settings", "git@github.com:joel/augram-settings.git")]
    [InlineData("/srv/git/settings.git", "/srv/git/other.git")]
    public void DifferentRepositoriesDoNotMatch(string first, string second)
    {
        Assert.False(RemoteUrl.SameRepository(first, second));
    }

    [Fact]
    public void WindowsPathsMatchWithEitherSlashAndAnyCase()
    {
        Assert.True(RemoteUrl.SameRepository(@"C:\Temp\Remote.git", "C:/Temp/Remote"));
        Assert.Equal(OperatingSystem.IsWindows(), RemoteUrl.SameRepository(@"C:\Temp\Remote.git", @"c:\temp\remote.git"));
    }

    [Theory]
    [InlineData("https://joel:token@github.com/joel/x.git", true)]
    [InlineData("https://ghp_token@github.com/joel/x.git", true)]
    [InlineData("http://joel@example.com/x.git", true)]
    [InlineData("ssh://git:secret@example.com/x.git", true)]
    [InlineData("https://github.com/joel/x.git", false)]
    [InlineData("ssh://git@github.com/joel/x.git", false)]
    [InlineData("git@github.com:joel/x.git", false)]
    [InlineData(@"C:\Temp\remote.git", false)]
    public void CredentialsInTheUrlAreDetected(string url, bool expected)
    {
        Assert.Equal(expected, RemoteUrl.CarriesCredentials(url));
    }
}
