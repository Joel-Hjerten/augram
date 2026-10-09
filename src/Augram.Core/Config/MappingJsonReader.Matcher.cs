using System.Text.Json.Nodes;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>The <c>matcher</c> of an app group or an ignored app, as <see cref="MappingJsonWriter"/> writes it (split out for size).</summary>
internal sealed partial class MappingJsonReader
{
    private static AppMatcher ReadMatcher(JsonNode? node, string where)
    {
        var what = $"'matcher' of {where}";
        var matcher = JsonMembers.RequireObject(node, what);
        return new AppMatcher
        {
            WindowsProcessNames = JsonMembers.OptionalStrings(matcher, "processNames", what),
            MacProcessNames = JsonMembers.OptionalStrings(matcher, "macProcessNames", what),
            ProcessPath = JsonMembers.OptionalString(matcher, "processPath", what),
            ProcessPathIsRegex = JsonMembers.OptionalBool(matcher, "processPathIsRegex", fallback: false, what),
            MacProcessPath = JsonMembers.OptionalString(matcher, "macProcessPath", what),
            MacProcessPathIsRegex = JsonMembers.OptionalBool(matcher, "macProcessPathIsRegex", fallback: false, what),
            Title = JsonMembers.OptionalString(matcher, "title", what),
            TitleIsRegex = JsonMembers.OptionalBool(matcher, "titleIsRegex", fallback: false, what),
            MacTitle = JsonMembers.OptionalString(matcher, "macTitle", what),
            MacTitleIsRegex = JsonMembers.OptionalBool(matcher, "macTitleIsRegex", fallback: false, what),
            WindowsProcessNamesAreRegex = JsonMembers.OptionalBool(matcher, "processNamesAreRegex", fallback: false, what),
            MacProcessNamesAreRegex = JsonMembers.OptionalBool(matcher, "macProcessNamesAreRegex", fallback: false, what),
            RootTitle = JsonMembers.OptionalString(matcher, "rootTitle", what),
            RootTitleIsRegex = JsonMembers.OptionalBool(matcher, "rootTitleIsRegex", fallback: false, what),
            ParentTitle = JsonMembers.OptionalString(matcher, "parentTitle", what),
            ParentTitleIsRegex = JsonMembers.OptionalBool(matcher, "parentTitleIsRegex", fallback: false, what),
            ControlTitle = JsonMembers.OptionalString(matcher, "controlTitle", what),
            ControlTitleIsRegex = JsonMembers.OptionalBool(matcher, "controlTitleIsRegex", fallback: false, what),
            OwnerClass = JsonMembers.OptionalString(matcher, "ownerClass", what),
            OwnerClassIsRegex = JsonMembers.OptionalBool(matcher, "ownerClassIsRegex", fallback: false, what),
            RootClass = JsonMembers.OptionalString(matcher, "rootClass", what),
            RootClassIsRegex = JsonMembers.OptionalBool(matcher, "rootClassIsRegex", fallback: false, what),
            ParentClass = JsonMembers.OptionalString(matcher, "parentClass", what),
            ParentClassIsRegex = JsonMembers.OptionalBool(matcher, "parentClassIsRegex", fallback: false, what),
            ControlClass = JsonMembers.OptionalString(matcher, "controlClass", what),
            ControlClassIsRegex = JsonMembers.OptionalBool(matcher, "controlClassIsRegex", fallback: false, what),
            ClassChain = JsonMembers.OptionalStrings(matcher, "classChain", what),
            IgnoreWhenFullScreen = JsonMembers.OptionalBool(matcher, "ignoreWhenFullScreen", fallback: false, what),
        };
    }
}
