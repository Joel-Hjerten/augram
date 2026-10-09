using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>The <c>matcher</c> of an app group or an ignored app (split out of <see cref="MappingJsonWriter"/> for size).</summary>
internal static partial class MappingJsonWriter
{
    /// <summary>The per-window fields (schema 3) are written only when set, with their toggle only when on, so a matcher without them looks as before.</summary>
    private static void WriteFieldIfSet(Utf8JsonWriter writer, string name, string? value, bool isRegex)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        writer.WriteString(name, value);
        WriteFlagIfSet(writer, name + "IsRegex", isRegex);
    }

    private static void WriteFlagIfSet(Utf8JsonWriter writer, string name, bool value)
    {
        if (value)
        {
            writer.WriteBoolean(name, true);
        }
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
        WriteFlagIfSet(writer, "processNamesAreRegex", matcher.WindowsProcessNamesAreRegex);
        if (matcher.MacProcessNames.Count > 0)
        {
            WriteStrings(writer, "macProcessNames", matcher.MacProcessNames);
        }

        WriteFlagIfSet(writer, "macProcessNamesAreRegex", matcher.MacProcessNamesAreRegex);

        writer.WriteString("processPath", matcher.ProcessPath);
        writer.WriteBoolean("processPathIsRegex", matcher.ProcessPathIsRegex);
        WriteFieldIfSet(writer, "macProcessPath", matcher.MacProcessPath, matcher.MacProcessPathIsRegex);
        writer.WriteString("title", matcher.Title);
        writer.WriteBoolean("titleIsRegex", matcher.TitleIsRegex);
        WriteFieldIfSet(writer, "macTitle", matcher.MacTitle, matcher.MacTitleIsRegex);
        WriteFieldIfSet(writer, "rootTitle", matcher.RootTitle, matcher.RootTitleIsRegex);
        WriteFieldIfSet(writer, "parentTitle", matcher.ParentTitle, matcher.ParentTitleIsRegex);
        WriteFieldIfSet(writer, "controlTitle", matcher.ControlTitle, matcher.ControlTitleIsRegex);
        WriteFieldIfSet(writer, "ownerClass", matcher.OwnerClass, matcher.OwnerClassIsRegex);
        WriteFieldIfSet(writer, "rootClass", matcher.RootClass, matcher.RootClassIsRegex);
        WriteFieldIfSet(writer, "parentClass", matcher.ParentClass, matcher.ParentClassIsRegex);
        WriteFieldIfSet(writer, "controlClass", matcher.ControlClass, matcher.ControlClassIsRegex);
        WriteStrings(writer, "classChain", matcher.ClassChain);
        writer.WriteBoolean("ignoreWhenFullScreen", matcher.IgnoreWhenFullScreen);
        writer.WriteEndObject();
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
}
