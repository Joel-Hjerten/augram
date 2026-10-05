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
    /// Upgrades the tree from <paramref name="version"/> to <paramref name="version"/> + 1.
    /// When schema 2 exists, its step goes here as <c>case 1:</c> and edits the nodes that
    /// changed (rename a property, move a section, fill a new member) and nothing else.
    /// </summary>
    private static void Step(JsonObject root, int version)
    {
        switch (version)
        {
            default:
                throw new ConfigFormatException($"No migration from schema version {version} to {version + 1} exists.");
        }
    }
}
