using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Tests.Mapping.Support;

/// <summary>Builders for windows, groups, commands and steps with fresh ids and sensible defaults.</summary>
internal static class MappingFixtures
{
    public static GestureId Up { get; } = GestureId.New();

    public static GestureId Down { get; } = GestureId.New();

    public static WindowIdentity Window(
        string processName = "chrome.exe",
        string? path = null,
        string? title = null,
        IReadOnlyList<string>? classChain = null,
        bool fullScreen = false,
        WindowLevels? levels = null)
        => new(Handle: 1, RootHandle: 1, processName, path, title, classChain ?? [], ProcessId: 42, fullScreen, IsDesktop: false) { Levels = levels ?? WindowLevels.None };

    public static AppMatcher ByProcess(params string[] names) => new() { WindowsProcessNames = names };

    /// <summary>An active group matching <c>&lt;name lower-cased&gt;.exe</c> unless a matcher is given.</summary>
    public static AppGroup NewGroup(string name, AppMatcher? matcher = null, params Command[] commands)
        => new(GroupId.New(), name, IsActive: true, SuppressGlobals: false, matcher ?? ByProcess(name.ToLowerInvariant() + ".exe"), commands);

    public static AppGroup NewGlobal(params Command[] commands) => AppGroup.EmptyGlobal with { Commands = commands };

    public static Command NewCommand(string name, Trigger? trigger = null, params CommandStep[] steps)
        => new(CommandId.New(), name, trigger ?? Trigger.None, IsActive: true, steps);

    public static Command NewCommand(string name, GestureId gesture, params CommandStep[] steps)
        => NewCommand(name, Trigger.ForGesture(gesture), steps);

    public static CommandCategory NewCategory(string name) => new(CategoryId.New(), name);

    /// <summary>The command, sorted into the category.</summary>
    public static Command In(this Command command, CommandCategory category) => command with { CategoryId = category.Id };

    public static CommandStep NewStep(string text, HostPlatform authoredOn = HostPlatform.Windows)
        => new(new FakeStep(text), authoredOn);

    public static MappingDocument Document(params AppGroup[] groups) => new(groups, []);
}
