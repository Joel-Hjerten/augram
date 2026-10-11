using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>ADR-0002 §5c: every field kind has a self-registered renderer, found by the naming convention <c>XField</c> → kind <c>X</c>.</summary>
public sealed class FieldKindCoverageTests
{
    [Fact]
    public void EveryFieldKindHasARenderer()
    {
        var kinds = typeof(Field).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Field).IsAssignableFrom(type))
            .Select(type => type.Name.Split('`')[0].Replace("Field", string.Empty, StringComparison.Ordinal))
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["ButtonRadio", "CheckList", "Color", "Custom", "Dropdown", "Links", "Note", "Number", "Pattern", "Slider", "Text", "Toggle", "Toggles"], kinds);
        foreach (var kind in kinds)
        {
            Assert.Contains(kind, FieldRendererRegistry.Default.Kinds);
        }
    }
}
