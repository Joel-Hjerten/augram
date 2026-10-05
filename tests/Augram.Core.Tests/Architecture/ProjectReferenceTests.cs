using System.Reflection;
using Xunit;

namespace Augram.Core.Tests.Architecture;

/// <summary>
/// ADR-0002 §1: dependencies point inward. Core references nothing but the BCL;
/// the importer references the BCL and Core. Loaded by name so the rule holds
/// even while the assemblies are empty.
/// </summary>
public sealed class ProjectReferenceTests
{
    [Fact]
    public void CoreReferencesOnlyTheBcl()
    {
        var core = Assembly.Load("Augram.Core");

        var violations = ReferencePolicy.Violations(core);

        Assert.True(violations.Count == 0, $"Augram.Core must not reference: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ImportReferencesOnlyTheBclAndCore()
    {
        var import = Assembly.Load("Augram.Import.StrokesPlus");

        var violations = ReferencePolicy.Violations(import, "Augram.Core");

        Assert.True(violations.Count == 0, $"Augram.Import.StrokesPlus must not reference: {string.Join(", ", violations)}");
    }
}
