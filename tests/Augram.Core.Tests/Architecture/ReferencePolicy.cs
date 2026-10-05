using System.Reflection;

namespace Augram.Core.Tests.Architecture;

/// <summary>
/// Decides which assembly references a project is allowed to have (ADR-0002 §1).
/// The BCL is allowed, named forbidden families never are, and each project may
/// add its own allowed Augram assemblies.
/// </summary>
internal static class ReferencePolicy
{
    private static readonly string[] BclExactNames = ["System", "netstandard", "mscorlib"];
    private static readonly string[] BclPrefixes = ["System.", "Microsoft."];
    private static readonly string[] ForbiddenPrefixes = ["Avalonia", "SharpHook", "System.Windows", "System.Drawing"];

    public static IReadOnlyList<string> Violations(Assembly assembly, params string[] allowedAugramAssemblies)
    {
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsAllowed(name, allowedAugramAssemblies))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsAllowed(string name, string[] allowedAugramAssemblies)
    {
        if (ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (allowedAugramAssemblies.Contains(name, StringComparer.Ordinal))
        {
            return true;
        }

        return BclExactNames.Contains(name, StringComparer.Ordinal)
            || BclPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }
}
