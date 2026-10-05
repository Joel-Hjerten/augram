namespace Augram.Core.Tests.Import;

/// <summary>
/// Locates synthetic StrokesPlus.net fixture files by walking up from the test assembly to the
/// repo root (<c>Augram.slnx</c>), so nothing needs copying to the output folder. Fixtures under
/// <c>Fixtures/StrokesPlusNet</c> are hand-written; never put a real SP.net config there.
/// </summary>
internal static class FixturePath
{
    private static readonly string Root = FindRoot();

    public static string StrokesPlusNet(string fileName)
        => Path.Combine(Root, "tests", "Augram.Core.Tests", "Fixtures", "StrokesPlusNet", fileName);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Augram.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Augram.slnx not found above " + AppContext.BaseDirectory);
    }
}
