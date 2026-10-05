using System.Reflection;
using Xunit;

namespace Augram.Engine.Tests;

/// <summary>Placeholder so the project runs under <c>dotnet test</c>; replaced by real tests in M1.</summary>
public sealed class ScaffoldTests
{
    [Fact]
    public void EngineAssemblyLoads()
    {
        var engine = Assembly.Load("Augram.Engine");

        Assert.Equal("Augram.Engine", engine.GetName().Name);
    }
}
