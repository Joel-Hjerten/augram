namespace Augram.App.Tests.Architecture;

/// <summary>Locates source folders from the test assembly by walking up to the solution root.</summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string ComponentsFolder => Path.Combine(Root, "src", "Augram.App", "Components");

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
