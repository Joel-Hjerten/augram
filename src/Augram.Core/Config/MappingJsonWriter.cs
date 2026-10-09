using System.Globalization;
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
/// when null), a group's <c>categories</c> (omitted when empty) and the <c>useOn</c> of a group, a category or a command
/// (omitted when every platform), so a diff after an edit shows only the
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

        WriteSteps(writer, command.Steps);
        if (command.OwnVersion is { } own)
        {
            // F8: the steps of the platform the command was not authored on; absent while that platform runs the converted original.
            writer.WritePropertyName("ownVersion");
            WriteOwnVersion(writer, own);
        }

        writer.WriteEndObject();
    }

    /// <summary>An own version as an object value (the caller writes the property name, if any).</summary>
    public static void WriteOwnVersion(Utf8JsonWriter writer, CommandVersion own)
    {
        writer.WriteStartObject();
        writer.WriteString("platform", own.Platform.ToString());
        writer.WriteString("basedOn", own.BasedOn);
        writer.WriteString("changedAt", own.ChangedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        if (own.Trigger is { } trigger)
        {
            // Omitted while the platform uses the original's trigger, converted; null when its own is unbound.
            WriteTrigger(writer, trigger);
        }

        WriteSteps(writer, own.Steps);
        writer.WriteEndObject();
    }

    private static void WriteSteps(Utf8JsonWriter writer, IReadOnlyList<CommandStep> steps)
    {
        writer.WriteStartArray("steps");
        foreach (var step in steps)
        {
            WriteStep(writer, step);
        }

        writer.WriteEndArray();
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
            WriteCategoryMembers(writer, category);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>A category's own members, <c>id</c>, <c>name</c> and <c>useOn</c> when it is not on every platform; the sync item writes the same after its group.</summary>
    public static void WriteCategoryMembers(Utf8JsonWriter writer, CommandCategory category)
    {
        writer.WriteString("id", category.Id.Value);
        writer.WriteString("name", category.Name);
        WriteUseOn(writer, category.UseOn);
    }

    /// <summary>
    /// <c>{ "gesture": id }</c>, <c>{ "wheel": "Up" }</c>, <c>{ "click": true }</c> or null, with a <c>hold</c> (schema 2) only when
    /// the trigger holds more than the stroke button alone, so a plain trigger reads as it did before combinations.
    /// </summary>
    private static void WriteTrigger(Utf8JsonWriter writer, Trigger trigger)
    {
        switch (trigger)
        {
            case Trigger.GestureTrigger gesture:
                writer.WriteStartObject("trigger");
                writer.WriteString("gesture", gesture.GestureId.Value);
                break;
            case Trigger.WheelTrigger wheel:
                writer.WriteStartObject("trigger");
                writer.WriteString("wheel", wheel.Direction.ToString());
                break;
            case Trigger.ClickTrigger:
                writer.WriteStartObject("trigger");
                writer.WriteBoolean("click", true);
                break;
            default:
                writer.WriteNull("trigger");
                return;
        }

        WriteHold(writer, trigger.Hold);
        writer.WriteEndObject();
    }

    private static void WriteHold(Utf8JsonWriter writer, TriggerHold hold)
    {
        if (hold.IsDefault)
        {
            return;
        }

        writer.WriteStartObject("hold");
        writer.WriteString("buttons", hold.Buttons.ToString());
        if (hold.Keys != KeyModifiers.None)
        {
            writer.WriteString("keys", hold.Keys.ToString());
        }

        if (hold.Capture != HoldCapture.Either)
        {
            writer.WriteString("capture", hold.Capture.ToString());
        }

        writer.WriteEndObject();
    }

    private static void WriteStep(Utf8JsonWriter writer, CommandStep step)
    {
        writer.WriteStartObject();
        writer.WriteString("type", step.Step.StoredKey);
        writer.WriteString("authoredOn", step.AuthoredOn.ToString());
        writer.WriteBoolean("isActive", step.IsActive);
        writer.WritePropertyName("params");
        step.Step.Type.Write(step.Step).WriteTo(writer);
        writer.WriteEndObject();
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
    /// F8 "Use on" of a group, a category or a command: written only when it is not every platform, so a file (and a sync
    /// item) without the member is unchanged by it; a list of target names, so specific machines can join it later without a
    /// new format.
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
