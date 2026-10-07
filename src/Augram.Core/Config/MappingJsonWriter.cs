using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Config;

/// <summary>
/// Writes the <c>mapping</c> member of the file (README: file shape). Written by hand rather than
/// through <see cref="ConfigJsonContext"/> because a step is polymorphic: its parameters are whatever
/// its <see cref="IStepType.Write"/> returns, under the envelope F8 names (<c>type</c>, <c>authoredOn</c>,
/// <c>isActive</c>, <c>params</c>, <c>overrides</c>). Every member is written in full except
/// <c>overrides</c> (omitted when there are none), <c>note</c> and a command's <c>category</c> (omitted
/// when null) and a group's <c>categories</c> (omitted when empty), so a diff after an edit shows only the
/// edit and a file without categories looks as it did before they existed. An override is always of
/// the same type as its step and is written as that type's parameters under the platform's camelCase
/// name (<c>windows</c>, <c>macOS</c>).
/// </summary>
internal static class MappingJsonWriter
{
    public const string UseOnWindows = "windows";
    public const string UseOnMacOS = "macos";

    public static void Write(Utf8JsonWriter writer, MappingDocument mapping)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("groups");
        foreach (var group in mapping.Groups)
        {
            WriteGroup(writer, group);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("ignored");
        foreach (var app in mapping.Ignored)
        {
            WriteIgnored(writer, app);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>The member name an override is stored under: the platform's name in camelCase.</summary>
    public static string PlatformKey(HostPlatform platform) => JsonNamingPolicy.CamelCase.ConvertName(platform.ToString());

    public static void WriteGroup(Utf8JsonWriter writer, AppGroup group)
    {
        writer.WriteStartObject();
        writer.WriteString("id", group.Id.Value);
        writer.WriteString("name", group.Name);
        writer.WriteBoolean("isActive", group.IsActive);
        writer.WriteBoolean("suppressGlobals", group.SuppressGlobals);
        WriteUseOn(writer, group.UseOn);
        WriteMatcher(writer, group.Matcher);
        WriteCategories(writer, group.Categories);
        writer.WriteStartArray("commands");
        foreach (var command in group.Commands)
        {
            WriteCommand(writer, command);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public static void WriteCommand(Utf8JsonWriter writer, Command command)
    {
        writer.WriteStartObject();
        writer.WriteString("id", command.Id.Value);
        writer.WriteString("name", command.Name);
        WriteTrigger(writer, command.Trigger);
        writer.WriteBoolean("isActive", command.IsActive);
        WriteUseOn(writer, command.UseOn);
        if (command.CategoryId is { } category)
        {
            writer.WriteString("category", category.Value);
        }

        if (command.Note is not null)
        {
            writer.WriteString("note", command.Note);
        }

        writer.WriteStartArray("steps");
        foreach (var step in command.Steps)
        {
            WriteStep(writer, step);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteCategories(Utf8JsonWriter writer, IReadOnlyList<CommandCategory> categories)
    {
        if (categories.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("categories");
        foreach (var category in categories)
        {
            writer.WriteStartObject();
            writer.WriteString("id", category.Id.Value);
            writer.WriteString("name", category.Name);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteTrigger(Utf8JsonWriter writer, Trigger trigger)
    {
        switch (trigger)
        {
            case Trigger.GestureTrigger gesture:
                writer.WriteStartObject("trigger");
                writer.WriteString("gesture", gesture.GestureId.Value);
                writer.WriteEndObject();
                break;
            case Trigger.WheelTrigger wheel:
                writer.WriteStartObject("trigger");
                writer.WriteString("wheel", wheel.Direction.ToString());
                writer.WriteEndObject();
                break;
            default:
                writer.WriteNull("trigger");
                break;
        }
    }

    private static void WriteStep(Utf8JsonWriter writer, CommandStep step)
    {
        writer.WriteStartObject();
        writer.WriteString("type", step.Step.Type.Key);
        writer.WriteString("authoredOn", step.AuthoredOn.ToString());
        writer.WriteBoolean("isActive", step.IsActive);
        writer.WritePropertyName("params");
        step.Step.Type.Write(step.Step).WriteTo(writer);
        if (step.HasOverrides)
        {
            writer.WriteStartObject("overrides");
            WriteOverride(writer, HostPlatform.Windows, step.WindowsOverride);
            WriteOverride(writer, HostPlatform.MacOS, step.MacOsOverride);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteOverride(Utf8JsonWriter writer, HostPlatform platform, IStep? step)
    {
        if (step is null)
        {
            return;
        }

        writer.WritePropertyName(PlatformKey(platform));
        step.Type.Write(step).WriteTo(writer);
    }

    private static void WriteMatcher(Utf8JsonWriter writer, AppMatcher? matcher)
    {
        if (matcher is null)
        {
            writer.WriteNull("matcher");
            return;
        }

        writer.WriteStartObject("matcher");
        WriteStrings(writer, "processNames", matcher.WindowsProcessNames);
        if (matcher.MacProcessNames.Count > 0)
        {
            WriteStrings(writer, "macProcessNames", matcher.MacProcessNames);
        }

        writer.WriteString("processPath", matcher.ProcessPath);
        writer.WriteBoolean("processPathIsRegex", matcher.ProcessPathIsRegex);
        writer.WriteString("title", matcher.Title);
        writer.WriteBoolean("titleIsRegex", matcher.TitleIsRegex);
        WriteStrings(writer, "classChain", matcher.ClassChain);
        writer.WriteBoolean("ignoreWhenFullScreen", matcher.IgnoreWhenFullScreen);
        writer.WriteEndObject();
    }

    /// <summary>
    /// F8 "Use on": written only when the group is not on every platform, so a file (and a sync item) without the member is
    /// unchanged by it; a list of target names, so specific machines can join it later without a new format.
    /// </summary>
    private static void WriteUseOn(Utf8JsonWriter writer, PlatformSet useOn)
    {
        if (useOn == PlatformSet.All)
        {
            return;
        }

        writer.WriteStartArray("useOn");
        if (useOn.HasFlag(PlatformSet.Windows))
        {
            writer.WriteStringValue(UseOnWindows);
        }

        if (useOn.HasFlag(PlatformSet.MacOS))
        {
            writer.WriteStringValue(UseOnMacOS);
        }

        writer.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    public static void WriteIgnored(Utf8JsonWriter writer, IgnoredApp app)
    {
        writer.WriteStartObject();
        writer.WriteString("id", app.Id.Value);
        writer.WriteString("name", app.Name);
        writer.WriteBoolean("isActive", app.IsActive);
        WriteMatcher(writer, app.Matcher);
        writer.WriteBoolean("disableEntirely", app.DisableEntirely);
        writer.WriteEndObject();
    }
}
