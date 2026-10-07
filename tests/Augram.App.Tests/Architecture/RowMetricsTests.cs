using System.Xml.Linq;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// One source of truth for list rows (Joel, 2026-10-07: the Apps tab's headers were taller than the
/// Global tab's because one row kept a toolkit default the other did not have). Every row component's
/// template in every theme uses the shared <c>Border.row</c> frame and <c>Grid.row-line</c> line, and
/// sets none of the spacing those styles own, so every list has the same line height and padding. The
/// metrics themselves are the <c>Row.*</c> tokens in <c>Themes/Tokens.axaml</c>.
/// </summary>
public sealed class RowMetricsTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    // Spacing the shared row styles own; a row template that sets one of these locally overrides the style.
    private static readonly string[] FrameOwned = ["Padding", "Background", "BorderBrush", "BorderThickness", "MinHeight", "Height"];
    private static readonly string[] LineOwned = ["Margin", "MinHeight", "Height"];

    // Not list rows: a settings form's label/editor row has its own metrics (Form.* tokens).
    private static readonly string[] NotListRows = ["FieldRow"];

    public static TheoryData<string> RowThemes()
    {
        var data = new TheoryData<string>();
        foreach (var (file, theme) in Themes())
        {
            data.Add(Path.GetFileName(file) + " " + (string)theme.Attribute("TargetType")!);
        }

        return data;
    }

    [Fact]
    public void EveryThemeHasRowComponents()
        => Assert.NotEmpty(Themes());

    [Theory]
    [MemberData(nameof(RowThemes))]
    public void RowTemplateUsesTheSharedFrameAndLineWithoutLocalSpacing(string row)
    {
        var theme = Themes().Single(entry => Path.GetFileName(entry.File) + " " + (string)entry.Theme.Attribute("TargetType")! == row).Theme;
        var elements = theme.Descendants().ToList();

        var frame = Assert.Single(elements, element => HasClass(element, "row"));
        Assert.Equal("Border", frame.Name.LocalName);
        var line = Assert.Single(elements, element => HasClass(element, "row-line"));
        Assert.Equal("Grid", line.Name.LocalName);
        Assert.Empty(frame.Attributes().Select(attribute => attribute.Name.LocalName).Intersect(FrameOwned));
        Assert.Empty(line.Attributes().Select(attribute => attribute.Name.LocalName).Intersect(LineOwned));
    }

    private static bool HasClass(XElement element, string name)
        => ((string?)element.Attribute("Classes"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name) == true;

    /// <summary>Every ControlTheme whose target type is a list row component (name ends in "Row", form rows excepted), in every theme file.</summary>
    private static List<(string File, XElement Theme)> Themes()
    {
        var folder = Path.Combine(RepositoryPaths.Root, "src", "Augram.App", "Themes");
        return Directory.EnumerateFiles(folder, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => XDocument.Load(file).Descendants(Avalonia + "ControlTheme").Select(theme => (file, theme)))
            .Where(entry => IsListRow((string?)entry.theme.Attribute("TargetType")))
            .ToList();
    }

    private static bool IsListRow(string? targetType)
    {
        if (targetType is null || !targetType.EndsWith("Row", StringComparison.Ordinal))
        {
            return false;
        }

        var name = targetType[(targetType.IndexOf(':', StringComparison.Ordinal) + 1)..];
        return !NotListRows.Contains(name);
    }
}
