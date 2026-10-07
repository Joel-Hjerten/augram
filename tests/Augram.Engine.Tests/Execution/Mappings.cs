using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Engine.Tests.Execution;

/// <summary>Builds small, valid mapping documents for executor tests; every group is active and every step authored on Windows.</summary>
internal static class Mappings
{
    public static Command Command(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, steps.Select(step => new CommandStep(step, HostPlatform.Windows)).ToArray());

    public static AppGroup Group(string name, string processName, bool suppressGlobals = false, params Command[] commands)
        => new(GroupId.New(), name, IsActive: true, suppressGlobals, new AppMatcher { ProcessNames = [processName] }, commands);

    /// <summary>Global with <paramref name="global"/>, plus <paramref name="groups"/>; validated and sorted as the store would.</summary>
    public static MappingDocument Document(IReadOnlyList<Command> global, params AppGroup[] groups)
        => MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal with { Commands = global }, .. groups], []));

    public static MappingDocument Global(params Command[] commands) => Document(commands);
}
