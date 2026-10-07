using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;

namespace Augram.App.Tests.Support;

/// <summary>Builds commands and groups for mapping-store tests: a command bound to a gesture, with one Delay step unless told otherwise.</summary>
internal static class MappingFixture
{
    public static Command Command(string name, GestureId gestureId, bool withStep = true, bool isActive = true)
        => new(CommandId.New(), name, Trigger.ForGesture(gestureId), isActive, withStep ? [new CommandStep(new DelayStep(30), HostPlatform.Windows)] : []);

    public static Command Unbound(string name)
        => new(CommandId.New(), name, Trigger.None, IsActive: true, [new CommandStep(new DelayStep(30), HostPlatform.Windows)]);

    /// <summary>A non-Global group with no matcher yet (valid: a group matches nothing until identified).</summary>
    public static AppGroup Group(string name, params Command[] commands)
        => new(GroupId.New(), name, IsActive: true, SuppressGlobals: false, Matcher: null, commands);
}
