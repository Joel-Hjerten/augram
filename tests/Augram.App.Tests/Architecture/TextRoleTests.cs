using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// The text rule (App README; Joel, 2026-10-11: "set down a couple of font stylings and then reuse those"): a text takes a
/// role class from <c>Themes/Default/Text.axaml</c>, which defines each role once, and never a size or weight of its own.
/// So no markup in the app but Text.axaml sets <c>FontSize</c> or <c>FontWeight</c> (as an attribute or a style setter),
/// no TextBlock carries the classes of two roles, and no code sets either property. The Wireframe theme is left out: it is
/// the older look, kept as a gallery option, and the Default theme's roles win over it.
/// </summary>
public sealed partial class TextRoleTests
{
    private const string UseARole =
        "Give the text a role class from Themes/Default/Text.axaml instead: heading (16 semibold: the command header's name, a dialog's title), "
        + "title (13 semibold: a panel's title, a name in a row), group (13 semibold muted: a form section's title above its card), "
        + "group-label (11 semibold muted: a small label over a group of choices or a column), "
        + "body (13: labels, values, notes; also every TextBlock without a class and every control's text), "
        + "secondary (11 faint: the line under a name, counts, help, placeholders), badge (10 semibold muted: tags), mono (the log); "
        + "a colour modifier (muted, faint, danger, warn, ok, accent) may follow the role.";

    // Code that draws text outside the theme: the F1 inspector paints its labels itself (FormattedText), over any theme.
    private static readonly string[] CodeAllowList = ["Inspector" + Path.DirectorySeparatorChar];

    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    private static string AppFolder => Path.Combine(RepositoryPaths.Root, "src", "Augram.App");

    private static string TextFile => Path.Combine(AppFolder, "Themes", "Default", "Text.axaml");

    [Fact]
    public void OnlyTheTextRolesSetAFontSizeOrWeightInMarkup()
    {
        var offenders = new List<string>();
        foreach (var file in RoledMarkup().Where(file => file != TextFile))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (FontAttribute().IsMatch(lines[index]) || FontSetter().IsMatch(lines[index]))
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "A font size or weight outside the text roles:" + Environment.NewLine + string.Join(Environment.NewLine, offenders) + Environment.NewLine + UseARole);
    }

    [Fact]
    public void NoCodeSetsAFontSizeOrWeight()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AppFolder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(AppFolder, file);
            if (IsBuildOutput(relative) || CodeAllowList.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (FontCode().IsMatch(lines[index]))
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Code that sets a font size or weight:" + Environment.NewLine + string.Join(Environment.NewLine, offenders) + Environment.NewLine + UseARole);
    }

    [Fact]
    public void EveryTextRoleHasItsClassesAndNoClassIsInTwoRoles()
    {
        var roles = RoleClasses();

        Assert.Equal(["heading", "title", "group", "group-label", "body", "secondary", "badge"], roles.Keys);
        var shared = roles.SelectMany(role => role.Value.Select(name => (name, role.Key))).GroupBy(entry => entry.name).Where(group => group.Count() > 1);
        Assert.Empty(shared.Select(group => $"{group.Key}: {string.Join(", ", group.Select(entry => entry.Key))}"));
    }

    [Fact]
    public void NoTextBlockInMarkupHasTwoRoles()
    {
        var roleOf = RoleClasses().SelectMany(role => role.Value.Select(name => (name, role.Key))).ToDictionary(entry => entry.name, entry => entry.Key);
        var offenders = new List<string>();
        foreach (var file in RoledMarkup())
        {
            foreach (var block in XDocument.Load(file).Descendants(Avalonia + "TextBlock"))
            {
                var classes = ((string?)block.Attribute("Classes"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
                var blockRoles = classes.Where(roleOf.ContainsKey).Select(name => roleOf[name]).Distinct().ToList();
                if (blockRoles.Count > 1)
                {
                    offenders.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}: Classes=\"{string.Join(' ', classes)}\" is {string.Join(" and ", blockRoles)}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "A TextBlock in two text roles (the later one in Text.axaml wins silently):" + Environment.NewLine + string.Join(Environment.NewLine, offenders) + Environment.NewLine + UseARole);
    }

    /// <summary>The app's markup that the text roles style: everything but the Wireframe theme.</summary>
    private static IEnumerable<string> RoledMarkup()
        => Directory.EnumerateFiles(AppFolder, "*.axaml", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(Path.GetRelativePath(AppFolder, file)))
            .Where(file => !file.StartsWith(Path.Combine(AppFolder, "Themes", "Wireframe"), StringComparison.Ordinal));

    /// <summary>
    /// Each role in Text.axaml: a style that sets a FontSize and selects TextBlock classes only, named by its first class (the
    /// role's own). Mono is left out: it selects the log's cells by their list too, on purpose over Body.
    /// </summary>
    private static Dictionary<string, List<string>> RoleClasses()
    {
        var roles = new Dictionary<string, List<string>>();
        foreach (var style in XDocument.Load(TextFile).Root!.Elements(Avalonia + "Style"))
        {
            var parts = ((string)style.Attribute("Selector")!).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var setsSize = style.Elements(Avalonia + "Setter").Any(setter => (string?)setter.Attribute("Property") == "FontSize");
            if (setsSize && parts.All(part => ClassOnly().IsMatch(part)))
            {
                var classes = parts.Select(part => part["TextBlock.".Length..]).ToList();
                roles[classes[0]] = classes;
            }
        }

        return roles;
    }

    private static bool IsBuildOutput(string relative)
        => relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    // TextBlock.some-class, nothing else.
    [GeneratedRegex(@"^TextBlock\.[\w-]+$")]
    private static partial Regex ClassOnly();

    // FontSize="…" or FontWeight="…", also attached (TextBlock.FontSize="…").
    [GeneratedRegex(@"\b(?:FontSize|FontWeight)\s*=")]
    private static partial Regex FontAttribute();

    // <Setter Property="FontSize" …> or "FontWeight", also attached ("TextElement.FontSize").
    [GeneratedRegex(@"Property=""(?:\w+\.)?(?:FontSize|FontWeight)""")]
    private static partial Regex FontSetter();

    // text.FontSize = 12, new TextBlock { FontWeight = … }, SetValue(TextBlock.FontSizeProperty, …); not LabelFontSize = 10.
    [GeneratedRegex(@"(?<!\w)(?:(?:FontSize|FontWeight)\s*=(?!=)|(?:FontSize|FontWeight)Property\b)")]
    private static partial Regex FontCode();
}
