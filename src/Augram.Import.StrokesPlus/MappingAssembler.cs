using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns the readers' groups and ignored apps into a valid <see cref="MappingDocument"/>. Each item
/// is checked on its own against <see cref="MappingRules"/> so one bad group drops with a warning and
/// the rest still import; the Global group can only ever fall back to empty, never disappear.
/// </summary>
internal static class MappingAssembler
{
    public static MappingDocument Assemble(IReadOnlyList<AppGroup> groups, IReadOnlyList<IgnoredApp> ignored, List<ImportWarning> warnings)
    {
        var acceptedGroups = new List<AppGroup>();
        foreach (var group in groups)
        {
            var normalised = MappingRules.Normalised(group);
            try
            {
                MappingRules.EnsureValid(normalised, acceptedGroups);
                acceptedGroups.Add(normalised);
            }
            catch (MappingValidationException exception)
            {
                var what = group.IsGlobal ? "Global commands dropped: " : "App group dropped: ";
                warnings.Add(new ImportWarning(ImportSeverity.Warning, group.Name, what + exception.Message));
                if (group.IsGlobal)
                {
                    acceptedGroups.Add(AppGroup.EmptyGlobal);
                }
            }
        }

        if (!acceptedGroups.Any(group => group.IsGlobal))
        {
            acceptedGroups.Insert(0, AppGroup.EmptyGlobal);
        }

        var acceptedIgnored = new List<IgnoredApp>();
        foreach (var app in ignored)
        {
            var normalised = MappingRules.Normalised(app);
            try
            {
                MappingRules.EnsureValid(normalised, acceptedIgnored);
                acceptedIgnored.Add(normalised);
            }
            catch (MappingValidationException exception)
            {
                warnings.Add(new ImportWarning(ImportSeverity.Warning, app.Name, "Ignored app dropped: " + exception.Message));
            }
        }

        return MappingRules.ValidDocument(new MappingDocument(acceptedGroups, acceptedIgnored));
    }
}
