using System.Text.RegularExpressions;
using System.Xml.Linq;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// The fakes of Core's ports have one home, tests/Augram.TestSupport (plan 0007 S16; docs/reference/one-home-guards.md): no
/// other test project implements a port or declares a type of the same name, no src project references test code, and
/// TestSupport stays test code over Core only (no xunit, so a fake never asserts).
/// </summary>
public sealed partial class OneHomeTestCodeTests
{
    private const string TestSupport = "tests/Augram.TestSupport/";

    private static IEnumerable<string> OtherTestCode
        => Code("tests").Where(file => !file.StartsWith(TestSupport, StringComparison.Ordinal));

    [Fact]
    public void OnlyTestSupportImplementsCorePorts()
    {
        var ports = typeof(IClock).Assembly.GetTypes()
            .Where(type => type.IsInterface && (type.Namespace == typeof(IClock).Namespace || type == typeof(IStepType) || type == typeof(IStep)))
            .Select(type => StripArity(type.Name))
            .ToHashSet(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var file in OtherTestCode)
        {
            var text = string.Join('\n', Lines(file));
            foreach (Match declaration in TypeDeclaration().Matches(text))
            {
                var implemented = BaseNames(declaration.Groups["bases"].Value).Where(ports.Contains).ToList();
                if (implemented.Count > 0)
                {
                    var line = text.AsSpan(0, declaration.Index).Count('\n') + 1;
                    offenders.Add($"{file}:{line}: {declaration.Groups["name"].Value} implements {string.Join(", ", implemented)}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "A fake of a Core port outside tests/Augram.TestSupport:" + Environment.NewLine + string.Join(Environment.NewLine, offenders) + Environment.NewLine + "Use (or extend) the fake in tests/Augram.TestSupport instead; a port without one gets it there (plan 0007 S16).");
    }

    [Fact]
    public void NoTestProjectRedeclaresATestSupportType()
    {
        var names = typeof(FakeClock).Assembly.GetTypes().Where(type => !type.IsNested && type.Namespace == typeof(FakeClock).Namespace).Select(type => StripArity(type.Name)).ToHashSet(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var file in OtherTestCode)
        {
            var text = string.Join('\n', Lines(file));
            foreach (Match declaration in TypeDeclaration().Matches(text))
            {
                if (names.Contains(declaration.Groups["name"].Value))
                {
                    offenders.Add($"{file}:{text.AsSpan(0, declaration.Index).Count('\n') + 1}: {declaration.Groups["name"].Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Types named like a tests/Augram.TestSupport type:" + Environment.NewLine + string.Join(Environment.NewLine, offenders) + Environment.NewLine + "Use the TestSupport type (a global using) instead of a copy (plan 0007 S16).");
    }

    [Fact]
    public void TestCodeIsReferencedOnlyByTests()
    {
        var offenders = new List<string>();
        foreach (var project in Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "tools"), "*.csproj", SearchOption.AllDirectories)))
        {
            offenders.AddRange(ProjectReferences(project).Where(reference => reference == "Augram.TestSupport" || reference.EndsWith(".Tests", StringComparison.Ordinal)).Select(reference => $"{Path.GetFileName(project)} -> {reference}"));
        }

        var support = Path.Combine(RepositoryPaths.Root, "tests", "Augram.TestSupport", "Augram.TestSupport.csproj");
        offenders.AddRange(ProjectReferences(support).Where(reference => reference != "Augram.Core").Select(reference => $"Augram.TestSupport -> {reference} (Core only)"));
        offenders.AddRange(PackageReferences(support).Where(package => package.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)).Select(package => $"Augram.TestSupport -> {package} (no xunit: a fake records, the test asserts)"));

        Assert.True(offenders.Count == 0, "Project references against the test-code layering (plan 0007 S16; tests/Augram.TestSupport/README.md):" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> ProjectReferences(string project)
        => XDocument.Load(project).Descendants("ProjectReference").Select(element => Path.GetFileNameWithoutExtension(((string)element.Attribute("Include")!).Replace('\\', '/')));

    private static IEnumerable<string> PackageReferences(string project)
        => XDocument.Load(project).Descendants("PackageReference").Select(element => (string)element.Attribute("Include")!);

    private static string StripArity(string name) => name.Split('`')[0];

    // "IFoo<T>, Bar.IBaz" -> IFoo, IBaz: the last part of each name, generic arguments left out.
    private static IEnumerable<string> BaseNames(string bases)
        => GenericArguments().Replace(bases, string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Split('(')[0].Split('.')[^1].Trim());

    // class|record|struct|interface Name<…>(primary ctor) : bases {  (the bases up to the body, a where clause or a ;)
    [GeneratedRegex(@"\b(?:class|record|struct|interface)\s+(?<name>\w+)(?:<[^>{]*>)?(?:\s*\((?:[^()]|\([^()]*\))*\))?\s*(?::\s*(?<bases>[^{;]+?))?\s*(?=\bwhere\b|[{;])")]
    private static partial Regex TypeDeclaration();

    [GeneratedRegex(@"<[^<>]*(?:<[^<>]*>[^<>]*)*>")]
    private static partial Regex GenericArguments();
}
