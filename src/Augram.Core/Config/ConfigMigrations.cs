using System.Text.Json.Nodes;

namespace Augram.Core.Config;

/// <summary>
/// Forward-only schema migrations (requirements F8), applied to the JSON tree before it is
/// deserialized so old files never need old model types. One step per version bump.
/// </summary>
public static class ConfigMigrations
{
    /// <summary>Upgrades <paramref name="root"/> in place from <paramref name="fromVersion"/> to the current version.</summary>
    public static JsonObject Migrate(JsonObject root, int fromVersion)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfLessThan(fromVersion, 1);

        for (int version = fromVersion; version < ConfigDocument.CurrentSchemaVersion; version++)
        {
            Step(root, version);
        }

        root["schemaVersion"] = ConfigDocument.CurrentSchemaVersion;
        return root;
    }

    /// <summary>
    /// Upgrades the tree from <paramref name="version"/> to <paramref name="version"/> + 1, editing the nodes that
    /// changed (rename a property, move a section, fill a new member) and nothing else.
    /// </summary>
    private static void Step(JsonObject root, int version)
    {
        switch (version)
        {
            case 1:
                // 1 → 2 (trigger combinations): every new member is optional and absent means what version 1 meant (the stroke
                // button alone, the converted original trigger), so a version 1 tree is already a valid version 2 tree. The bump
                // exists so that a version 1 build refuses a version 2 file instead of dropping its combinations.
                break;
            case 2:
                // 2 → 3 (the StrokesPlus.net app definition's fields, each with Use Regex, Joel 2026-10-09): the new matcher
                // members are optional and absent means not consulted, as in version 2. The bump exists so that a version 2
                // build refuses a version 3 file instead of ignoring a root title or a class and matching too many windows.
                break;
            default:
                throw new ConfigFormatException($"No migration from schema version {version} to {version + 1} exists.");
        }
    }
}
