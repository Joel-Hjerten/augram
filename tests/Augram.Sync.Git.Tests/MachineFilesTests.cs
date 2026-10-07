using Xunit;

namespace Augram.Sync.Git.Tests;

public sealed class MachineFilesTests
{
    [Theory]
    [InlineData("machine-a")]
    [InlineData("Joel-Work-PC-3f2a")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void PlainIdsAreValid(string machineId)
    {
        Assert.True(MachineFiles.IsValidId(machineId));
        Assert.Equal($"machines/{machineId}.json", MachineFiles.RelativePath(machineId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData("a.b")]
    [InlineData("a b")]
    [InlineData("a_b")]
    [InlineData("jöel")]
    [InlineData("nul")]
    [InlineData("COM1")]
    public void IdsThatAreNotPlainPortableFileNamesAreInvalid(string? machineId)
    {
        Assert.False(MachineFiles.IsValidId(machineId));
    }

    [Fact]
    public void AnOverlongIdIsInvalid()
    {
        Assert.True(MachineFiles.IsValidId(new string('a', MachineFiles.MaxIdLength)));
        Assert.False(MachineFiles.IsValidId(new string('a', MachineFiles.MaxIdLength + 1)));
    }

    [Fact]
    public void ReadingAFolderThatDoesNotExistGivesNothing()
    {
        Assert.Empty(MachineFiles.ReadAll(Path.Combine(Path.GetTempPath(), "augram-sync-tests", "no-such-clone-" + Guid.NewGuid().ToString("N"))));
    }
}
