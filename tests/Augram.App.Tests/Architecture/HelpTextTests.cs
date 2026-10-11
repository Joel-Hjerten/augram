using System.Text.RegularExpressions;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// The help rule (App README; Joel, 2026-10-09): standing instructions are an ⓘ with a tooltip after a title, never a line
/// of text that takes room. A theme template may still show short state text in the <c>help</c> style (a count, a status,
/// "Draw here") and an empty state shown only while there is nothing else (hidden by <c>BoolConverters.Not</c> once there
/// is), so this fails on a literal sentence: a <c>help</c> TextBlock whose literal text has more than eight words, or one
/// bound to a <c>HelpText</c> property.
/// </summary>
public sealed partial class HelpTextTests
{
    [Fact]
    public void ThemesShowNoInstructionsAsTextLines()
    {
        var themes = Path.Combine(RepositoryPaths.Root, "src", "Augram.App", "Themes");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(themes, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in HelpBlock().Matches(text))
            {
                if (match.Value.Contains("BoolConverters.Not", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = match.Groups["text"].Value;
                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (value.Contains("HelpText", StringComparison.Ordinal) || (!value.StartsWith('{') && words > 8))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {value}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    // help, or secondary: the text role it is (Themes/Default/Text.axaml), so the rule holds under either class.
    [GeneratedRegex("""<TextBlock[^>]*Classes="(?:help|secondary)"[^>]*Text="(?<text>[^"]*)"[^>]*>""", RegexOptions.Singleline)]
    private static partial Regex HelpBlock();
}
