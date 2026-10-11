using Augram.Core.Mapping;
using Xunit;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// The one rule for a name that clashes (Joel, 2026-10-11): the name itself when free, else "name (2)", "name (3)"…, compared
/// as the rules compare names. A paste, a picked app, the sync and both imports rename through <see cref="NameScope"/>.
/// </summary>
public sealed class NameScopeTests
{
    [Fact]
    public void AFreeNameIsKept()
    {
        Assert.Equal("Orbit", NameScope.Free("Orbit", ["Pan", "Zoom"]));
    }

    [Fact]
    public void ATakenNameGetsTheFirstFreeNumber()
    {
        Assert.Equal("Orbit (2)", NameScope.Free("Orbit", ["Orbit"]));
        Assert.Equal("Orbit (3)", NameScope.Free("Orbit", ["Orbit", "Orbit (2)"]));
    }

    [Fact]
    public void NamesClashWhateverTheirCase()
    {
        Assert.Equal("orbit (2)", NameScope.Free("orbit", ["Orbit"]));
    }

    [Fact]
    public void EachClaimTakesItsName()
    {
        var scope = new NameScope(["Orbit"]);

        Assert.Equal(["Orbit (2)", "Orbit (3)", "Pan"], new[] { scope.Claim("Orbit"), scope.Claim("Orbit"), scope.Claim("Pan") });
    }
}
