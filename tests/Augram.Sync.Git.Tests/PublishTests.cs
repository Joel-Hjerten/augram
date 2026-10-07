using Xunit;

namespace Augram.Sync.Git.Tests;

public sealed class PublishTests : IDisposable
{
    private readonly GitSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void TheFirstPublishToAnEmptyRemoteCreatesMainWithTheFileAsWritten()
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.PreparedMachine("machine-a", remote);

        GitSandbox.AssertOk(machine.Publish("machine-a", "{\r\n  \"name\": \"Zig\"\r\n}\r\n", "machine-a: first sync"));

        Assert.Equal("machine-a: first sync", _sandbox.Git(remote, "log", "-1", "--format=%s", "main"));
        Assert.Equal("machines/machine-a.json", _sandbox.Git(remote, "ls-tree", "-r", "--name-only", "main"));
        var bytes = File.ReadAllBytes(Path.Combine(machine.Folder, "machines", "machine-a.json"));
        Assert.Equal("{\n  \"name\": \"Zig\"\n}\n"u8.ToArray(), bytes);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("master")]
    public void ASecondMachineClonesAndBothReadEachOthersFiles(string remoteDefaultBranch)
    {
        var remote = _sandbox.CreateRemote(defaultBranch: remoteDefaultBranch);
        var first = _sandbox.PreparedMachine("machine-a", remote);
        GitSandbox.AssertOk(first.Publish("machine-a", "{\"from\":\"a\"}\n", "a"));

        var second = _sandbox.PreparedMachine("machine-b", remote);
        Assert.Equal("refs/heads/main", _sandbox.Git(second.Folder, "symbolic-ref", "HEAD"));
        Assert.Equal(["machine-a"], second.ReadMachineFiles().Keys);
        GitSandbox.AssertOk(second.Publish("machine-b", "{\"from\":\"b\"}\n", "b"));
        GitSandbox.AssertOk(first.Pull());

        foreach (var machine in new[] { first, second })
        {
            var files = machine.ReadMachineFiles();
            Assert.Equal(2, files.Count);
            Assert.Equal("{\"from\":\"a\"}\n", files["machine-a"]);
            Assert.Equal("{\"from\":\"b\"}\n", files["machine-b"]);
        }
    }

    [Fact]
    public void PublishingUnchangedContentMakesNoCommit()
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.PreparedMachine("machine-a", remote);
        GitSandbox.AssertOk(machine.Publish("machine-a", "{\"v\":1}\n", "one"));

        GitSandbox.AssertOk(machine.Publish("machine-a", "{\"v\":1}\r\n", "two"));

        Assert.Equal("1", _sandbox.Git(remote, "rev-list", "--count", "main"));
        Assert.Equal("1", _sandbox.Git(machine.Folder, "rev-list", "--count", "HEAD"));
    }

    [Fact]
    public void APushRejectedBecauseAnotherMachinePublishedFirstIsPulledAndRetried()
    {
        var remote = _sandbox.CreateRemote();
        var first = _sandbox.PreparedMachine("machine-a", remote);
        var second = _sandbox.PreparedMachine("machine-b", remote);

        GitSandbox.AssertOk(first.Publish("machine-a", "{\"from\":\"a\"}\n", "a"));
        GitSandbox.AssertOk(second.Publish("machine-b", "{\"from\":\"b\"}\n", "b")); // never pulled first

        Assert.Equal("2", _sandbox.Git(remote, "rev-list", "--count", "main"));
        Assert.Equal(
            "machines/machine-a.json\nmachines/machine-b.json",
            _sandbox.Git(remote, "ls-tree", "-r", "--name-only", "main").ReplaceLineEndings("\n"));
        Assert.Equal("b", _sandbox.Git(remote, "log", "-1", "--format=%s", "main"));
        GitSandbox.AssertOk(first.Pull());
        Assert.Equal(2, first.ReadMachineFiles().Count);
    }

    [Fact]
    public void BothMachinesWithHistoryPublishingInTurnWithoutPullingStayInStep()
    {
        var remote = _sandbox.CreateRemote();
        var first = _sandbox.PreparedMachine("machine-a", remote);
        GitSandbox.AssertOk(first.Publish("machine-a", "{\"v\":1}\n", "a1"));
        var second = _sandbox.PreparedMachine("machine-b", remote);

        for (var round = 2; round <= 4; round++)
        {
            GitSandbox.AssertOk(first.Publish("machine-a", $"{{\"v\":{round}}}\n", $"a{round}"));
            GitSandbox.AssertOk(second.Publish("machine-b", $"{{\"v\":{round}}}\n", $"b{round}"));
        }

        Assert.Equal("7", _sandbox.Git(remote, "rev-list", "--count", "main"));
        Assert.Equal("{\"v\":4}\n", second.ReadMachineFiles()["machine-a"]);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("..\\b")]
    [InlineData("../b")]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("a.json")]
    [InlineData("CON")]
    public void AMachineIdThatIsNotAPlainFileNameIsRefused(string machineId)
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.PreparedMachine("machine-a", remote);

        var result = machine.Publish(machineId, "{}", "bad");

        Assert.False(result.Succeeded);
        Assert.Contains("not a usable machine id", result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(machine.Folder, "machines")));
        Assert.Equal(["global.gitconfig", "machine-a", "remote.git"], TopLevelNames());
    }

    private string[] TopLevelNames() =>
        Directory.EnumerateFileSystemEntries(_sandbox.Root).Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.Ordinal).ToArray();
}
