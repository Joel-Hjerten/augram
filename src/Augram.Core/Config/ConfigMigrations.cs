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
            case 1 or 2 or 3 or 4 or 5 or 6 or 7:
                // 1 → 2 up to 7 → 8 change no node: every member each bump added is optional and its absence means what the older
                // version meant (a schema 5 notIn names no Per command entry, so MappingRules drops it on load, plan 0004 decision
                // 6; a schema 7 Hotkey step has no layouts, plan 0011). Each bump exists so an older build refuses the newer file.
                // What each added: Config/README.md, "Schema history".
                break;
            default:
                throw new ConfigFormatException($"No migration from schema version {version} to {version + 1} exists.");
        }
    }
}
