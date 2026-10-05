namespace Augram.Engine.Tests.Diagnostics;

/// <summary>A fresh directory under the system temp folder, removed on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "augram-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public IReadOnlyList<string> FileNames() => Directory.GetFiles(Path).Select(p => System.IO.Path.GetFileName(p)).Order(StringComparer.Ordinal).ToList();

    /// <summary>Reads a file another handle may still hold open for writing, the way a log viewer does.</summary>
    public static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Left for the OS to clean; a test must never fail on teardown.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
