using Augram.App.Navigation;
using Augram.Core.Mapping;

namespace Augram.App.Tests.Support;

public sealed class FakeCommandLocator : ICommandLocator
{
    public List<CommandId> Shown { get; } = [];

    public bool Result { get; set; } = true;

    public bool ShowCommand(CommandId id)
    {
        Shown.Add(id);
        return Result;
    }
}
