using System.Text.Json.Nodes;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>Parses a JSON object literal for a <c>Read</c> call; the test reads as the config would.</summary>
internal static class StepJson
{
    public static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();
}
