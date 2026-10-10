using Augram.Platform.Windows.Startup;

namespace Augram.Platform.Windows.Tests.Startup;

/// <summary>The two registry values in memory, with every write and delete recorded.</summary>
internal sealed class FakeStartupRegistry : IWin32StartupRegistry
{
    public Dictionary<string, string> Commands { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, byte[]> Approvals { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Calls { get; } = [];

    public string? ReadCommand(string name) => Commands.GetValueOrDefault(name);

    public void WriteCommand(string name, string command)
    {
        Calls.Add($"write {name}");
        Commands[name] = command;
    }

    public void DeleteCommand(string name)
    {
        Calls.Add($"delete {name}");
        Commands.Remove(name);
    }

    public byte[]? ReadApproval(string name) => Approvals.GetValueOrDefault(name);

    public void DeleteApproval(string name)
    {
        Calls.Add($"delete approval {name}");
        Approvals.Remove(name);
    }
}
