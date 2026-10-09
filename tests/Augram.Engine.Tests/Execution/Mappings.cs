using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Engine.Tests.Execution;

/// <summary>
/// Builds small, valid mapping documents for executor tests; every group is active. <see cref="Command"/> authors its steps
/// on Windows; <see cref="CommandHere"/> on the platform the executor runs for, which a platform-bound step needs (a Run
/// step does not carry over, a Scroll step's Ctrl becomes Cmd), or the test fails on the macOS runner only.
/// </summary>
internal static class Mappings
{
    /// <summary>The platform the executor runs steps for in these tests (the null window operations' answer).</summary>
    public static HostPlatform Here => NullWindowOperations.Instance.Platform;

    public static Command Command(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, steps.Select(step => new CommandStep(step, HostPlatform.Windows)).ToArray());

    /// <summary>A command whose steps are authored where the test runs, so they run exactly as written on either runner.</summary>
    public static Command CommandHere(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, steps.Select(step => new CommandStep(step, Here)).ToArray());

    public static AppGroup Group(string name, string processName, bool suppressGlobals = false, params Command[] commands)
        => new(GroupId.New(), name, IsActive: true, suppressGlobals, new AppMatcher { WindowsProcessNames = [processName] }, commands);

    /// <summary>Global with <paramref name="global"/>, plus <paramref name="groups"/>; validated and sorted as the store would.</summary>
    public static MappingDocument Document(IReadOnlyList<Command> global, params AppGroup[] groups)
        => MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal with { Commands = global }, .. groups], []));

    public static MappingDocument Global(params Command[] commands) => Document(commands);
}
