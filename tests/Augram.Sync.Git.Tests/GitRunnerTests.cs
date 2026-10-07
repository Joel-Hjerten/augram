using System.Diagnostics;
using Xunit;

namespace Augram.Sync.Git.Tests;

/// <summary>
/// The process rules, observed from inside git: a shell alias (<c>!env</c>, <c>!sleep</c>) runs as
/// git's child, so it sees git's environment and is part of the tree a timeout must kill.
/// </summary>
public sealed class GitRunnerTests : IDisposable
{
    private readonly GitSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void GitRunsWithPromptsOffEnglishMessagesNoAskPassAndACeiling()
    {
        var result = _sandbox.Runner.Run(_sandbox.Root, GitRunner.LocalTimeout, ["-c", "alias.show-env=!env", "show-env"]);

        Assert.True(result.Succeeded, result.Error);
        var lines = result.Output.ReplaceLineEndings("\n").Split('\n');
        Assert.Contains("GIT_TERMINAL_PROMPT=0", lines);
        Assert.Contains("LC_ALL=C", lines);
        Assert.Contains(lines, line => line.StartsWith("GIT_CEILING_DIRECTORIES=", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("GIT_ASKPASS=", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("GIT_DIR=", StringComparison.Ordinal));
    }

    [Fact]
    public void ALateCallIsStoppedWithItsWholeProcessTree()
    {
        var clock = Stopwatch.StartNew();

        var result = _sandbox.Runner.Run(_sandbox.Root, TimeSpan.FromSeconds(1), ["-c", "alias.slow=!sleep 30", "slow"]);

        Assert.Equal(GitRunStatus.TimedOut, result.Status);
        Assert.False(result.Succeeded);
        // A surviving "sleep" would hold the output pipes open until the drain wait gives up.
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), $"took {clock.Elapsed}");
    }

    [Fact]
    public void AMissingExecutableIsReportedNotThrown()
    {
        var runner = new GitRunner(Path.Combine(_sandbox.Root, "no-such-git"));

        var result = runner.Run(_sandbox.Root, GitRunner.LocalTimeout, ["--version"]);

        Assert.Equal(GitRunStatus.NotInstalled, result.Status);
    }
}
