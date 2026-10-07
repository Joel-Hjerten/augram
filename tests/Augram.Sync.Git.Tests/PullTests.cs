using Xunit;

namespace Augram.Sync.Git.Tests;

public sealed class PullTests : IDisposable
{
    private readonly GitSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void PullFromARemoteWithNoBranchYetDoesNothing()
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.PreparedMachine("machine-a", remote);

        GitSandbox.AssertOk(machine.Pull());

        Assert.Empty(machine.ReadMachineFiles());
    }

    [Fact]
    public void PullIntoAMachineThatHasNotPublishedYetTakesTheRemoteFiles()
    {
        var remote = _sandbox.CreateRemote();
        var first = _sandbox.PreparedMachine("machine-a", remote);
        var second = _sandbox.PreparedMachine("machine-b", remote);
        GitSandbox.AssertOk(second.Publish("machine-b", "{\"from\":\"b\"}\n", "b"));

        GitSandbox.AssertOk(first.Pull());

        Assert.Equal("{\"from\":\"b\"}\n", first.ReadMachineFiles()["machine-b"]);
        GitSandbox.AssertOk(first.Publish("machine-a", "{\"from\":\"a\"}\n", "a"));
        Assert.Equal("2", _sandbox.Git(remote, "rev-list", "--count", "main"));
    }

    [Fact]
    public void PullKeepsTheUnpushedOwnCommitOnTopAndTheNextPublishPushesIt()
    {
        var remote = _sandbox.CreateRemote();
        var first = _sandbox.PreparedMachine("machine-a", remote);
        var second = _sandbox.PreparedMachine("machine-b", remote);
        GitSandbox.AssertOk(first.Publish("machine-a", "{\"v\":1}\n", "a1"));
        GitSandbox.AssertOk(second.Publish("machine-b", "{\"v\":1}\n", "b1"));

        // machine-b goes offline: its commit is made but the push fails.
        var away = remote + ".away";
        Directory.Move(remote, away);
        var offline = second.Publish("machine-b", "{\"v\":2}\n", "b2");
        Directory.Move(away, remote);
        Assert.False(offline.Succeeded);
        Assert.Equal(GitFailure.NotFound, offline.Error);

        GitSandbox.AssertOk(first.Publish("machine-a", "{\"v\":2}\n", "a2"));
        GitSandbox.AssertOk(second.Pull());

        Assert.Equal("b2\na2\nb1\na1", _sandbox.Git(second.Folder, "log", "--format=%s").ReplaceLineEndings("\n"));
        Assert.Equal("{\"v\":2}\n", second.ReadMachineFiles()["machine-a"]);
        Assert.Equal("{\"v\":2}\n", second.ReadMachineFiles()["machine-b"]);

        // Unchanged content, but the pending commit still goes out.
        GitSandbox.AssertOk(second.Publish("machine-b", "{\"v\":2}\n", "b2 again"));
        Assert.Equal("b2\na2\nb1\na1", _sandbox.Git(remote, "log", "--format=%s", "main").ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ReadMachineFilesSkipsWhatIsNotAMachineFile()
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.PreparedMachine("machine-a", remote);
        var folder = Path.Combine(machine.Folder, "machines");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a machine");
        File.WriteAllText(Path.Combine(folder, "machine-b.json.tmp"), "half written");
        Directory.CreateDirectory(Path.Combine(folder, "nested.json"));
        File.WriteAllText(Path.Combine(folder, "machine-c.json"), "{\"from\":\"c\"}");

        var files = machine.ReadMachineFiles();

        Assert.Equal(["machine-c"], files.Keys);
        Assert.Equal("{\"from\":\"c\"}", files["machine-c"]);
    }
}
