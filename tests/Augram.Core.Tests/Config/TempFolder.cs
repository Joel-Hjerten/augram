namespace Augram.Core.Tests.Config;

/// <summary>A unique folder under the OS temp path, deleted on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "augram-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative) => System.IO.Path.Combine(Path, relative);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp folder must not fail the test.
        }
    }
}
