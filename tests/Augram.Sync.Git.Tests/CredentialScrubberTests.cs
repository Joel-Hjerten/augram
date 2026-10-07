using Xunit;

namespace Augram.Sync.Git.Tests;

public sealed class CredentialScrubberTests
{
    [Theory]
    [InlineData("https://user:token@github.com/joel/x.git", "https://github.com/joel/x.git")]
    [InlineData("https://ghp_token@github.com/joel/x.git", "https://github.com/joel/x.git")]
    [InlineData("https://user:p@ss@github.com/joel/x.git", "https://github.com/joel/x.git")]
    [InlineData("https://user:token@github.com", "https://github.com")]
    [InlineData("fatal: unable to access 'http://a:b@host:8080/r/': boom", "fatal: unable to access 'http://host:8080/r/': boom")]
    [InlineData("two: https://a:b@one.example/x and ssh://git@two.example/y", "two: https://one.example/x and ssh://two.example/y")]
    public void UserInfoIsRemovedFromEveryUrl(string text, string expected)
    {
        Assert.Equal(expected, CredentialScrubber.Scrub(text));
    }

    [Theory]
    [InlineData("https://github.com/joel/x.git")]
    [InlineData("git@github.com:joel/x.git")]
    [InlineData("mail joel@example.com about https://github.com/joel/x")]
    [InlineData("C:/Users/joel/AppData/Roaming/Augram/sync/repo")]
    public void TextWithoutUserInfoIsUnchanged(string text)
    {
        Assert.Equal(text, CredentialScrubber.Scrub(text));
    }
}
