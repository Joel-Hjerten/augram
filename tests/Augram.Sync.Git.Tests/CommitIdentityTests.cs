using Xunit;

namespace Augram.Sync.Git.Tests;

/// <summary>
/// Each test owns its sandbox so it controls the (isolated) global config: empty means no identity
/// is configured anywhere git looks; the developer's real config is never visible.
/// </summary>
public sealed class CommitIdentityTests
{
    [Fact]
    public void WithNoIdentityConfiguredTheFallbackIsUsedForThatCommandOnly()
    {
        using var sandbox = new GitSandbox();
        var remote = sandbox.CreateRemote();
        var machine = sandbox.Machine("machine-a", label: "test-pc");
        GitSandbox.AssertOk(machine.Prepare(remote));

        GitSandbox.AssertOk(machine.Publish("machine-a", "{}\n", "first"));

        Assert.Equal("Augram (test-pc)|augram@localhost", sandbox.Git(remote, "log", "-1", "--format=%an|%ae", "main"));
        Assert.Equal("Augram (test-pc)|augram@localhost", sandbox.Git(remote, "log", "-1", "--format=%cn|%ce", "main"));
        var local = sandbox.Runner.Run(machine.Folder, GitRunner.LocalTimeout, ["config", "--get", "user.name"]);
        Assert.False(local.Succeeded, "the fallback must not be written to the clone's config");
        Assert.Equal(string.Empty, File.ReadAllText(sandbox.GlobalConfig));
    }

    [Fact]
    public void AConfiguredIdentityIsUsed()
    {
        using var sandbox = new GitSandbox("[user]\n\tname = Configured Person\n\temail = configured@example.com\n");
        var remote = sandbox.CreateRemote();
        var machine = sandbox.Machine("machine-a", label: "test-pc");
        GitSandbox.AssertOk(machine.Prepare(remote));

        GitSandbox.AssertOk(machine.Publish("machine-a", "{}\n", "first"));

        Assert.Equal("Configured Person|configured@example.com", sandbox.Git(remote, "log", "-1", "--format=%an|%ae", "main"));
    }

    [Fact]
    public void ARebaseAfterARejectedPushAlsoWorksWithNoIdentityConfigured()
    {
        using var sandbox = new GitSandbox();
        var remote = sandbox.CreateRemote();
        var first = sandbox.PreparedMachine("machine-a", remote);
        GitSandbox.AssertOk(first.Publish("machine-a", "{}\n", "a"));
        var second = sandbox.PreparedMachine("machine-b", remote);
        GitSandbox.AssertOk(first.Publish("machine-a", "{\"v\":2}\n", "a2"));

        GitSandbox.AssertOk(second.Publish("machine-b", "{}\n", "b"));

        Assert.Equal("Augram (machine-b)", sandbox.Git(remote, "log", "-1", "--format=%cn", "main"));
    }

    [Theory]
    [InlineData("test-pc", "Augram (test-pc)")]
    [InlineData("  Work <PC>\n", "Augram (Work PC)")]
    [InlineData("", "Augram")]
    [InlineData(null, "Augram")]
    public void TheFallbackNameDropsWhatGitRefuses(string? label, string expected)
    {
        Assert.Equal(expected, CommitIdentity.FallbackName(label));
    }
}
