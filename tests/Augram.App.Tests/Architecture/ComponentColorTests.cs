using System.Text.RegularExpressions;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// ADR-0002 §5b: components reference theme tokens, never literal colours, or they
/// cannot be re-skinned. Scans every .axaml under src/Augram.App/Components/.
/// </summary>
public sealed partial class ComponentColorTests
{
    [Fact]
    public void ComponentsContainNoLiteralColors()
    {
        var offenders = new List<string>();

        foreach (var file in ComponentFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (HexColor().IsMatch(lines[index]) || NamedColorAttribute().IsMatch(lines[index]))
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Literal colours in components:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> ComponentFiles()
    {
        var folder = RepositoryPaths.ComponentsFolder;
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.axaml", SearchOption.AllDirectories)
            : [];
    }

    // #RGB, #ARGB, #RRGGBB, #AARRGGBB anywhere in the markup.
    [GeneratedRegex(@"#(?:[0-9A-Fa-f]{3,4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\b")]
    private static partial Regex HexColor();

    // A colour-carrying attribute whose value is a bare word (Red, Transparent) rather than a {markup extension}.
    [GeneratedRegex(@"\b(?:Color|Brush|Background|Foreground|BorderBrush|Fill|Stroke)=""[A-Za-z]+""")]
    private static partial Regex NamedColorAttribute();
}
