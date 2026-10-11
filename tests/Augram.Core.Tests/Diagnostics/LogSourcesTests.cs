using System.Reflection;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.Core.Tests.Diagnostics;

/// <summary><see cref="LogSources.All"/> is the one list of sources: every constant once, alphabetical, short and lowercase.</summary>
public sealed class LogSourcesTests
{
    private static readonly string[] Constants = typeof(LogSources)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void All_ListsEveryConstantOnce()
    {
        Assert.Equal(Constants.Order(StringComparer.Ordinal), LogSources.All.Order(StringComparer.Ordinal));
        Assert.Equal(LogSources.All.Count, LogSources.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void All_IsAlphabetical()
    {
        Assert.Equal(LogSources.All.Order(StringComparer.Ordinal), LogSources.All);
    }

    [Fact]
    public void Sources_AreShortLowercaseWords()
    {
        Assert.All(LogSources.All, s => Assert.Matches("^[a-z]{1,11}$", s));
    }
}
