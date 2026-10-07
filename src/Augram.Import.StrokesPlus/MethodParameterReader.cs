using System.Globalization;
using System.Text.Json;
using Augram.Core.Steps.Imported;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads a step's <c>MethodParameters[]</c> into name → value text (first name wins), the shape an
/// <see cref="ImportedStep"/> keeps, plus the typed lookups the mapped step types need. SP.net writes
/// a value as whatever type the method takes (an int for <c>Delay</c>, a bool for
/// <c>ConsumePhysicalInput</c>, an object for <c>SendHotKey</c>); the text keeps all of them.
/// </summary>
internal static class MethodParameterReader
{
    public static IReadOnlyDictionary<string, string> Read(JsonElement step)
    {
        if (!JsonRead.TryArray(step, StrokesPlusJson.Step.MethodParameters, out var array))
        {
            return ImportedStep.NoParameters;
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in array.EnumerateArray())
        {
            var name = JsonRead.Text(parameter, StrokesPlusJson.MethodParameter.Name);
            if (name.Length == 0 || parameters.ContainsKey(name))
            {
                continue;
            }

            parameters[name] = JsonRead.ValueText(JsonRead.Member(parameter, StrokesPlusJson.MethodParameter.Value));
        }

        return parameters.Count == 0 ? ImportedStep.NoParameters : parameters;
    }

    /// <summary>A parameter as an integer (a whole number written as "60" or "60.0"); false when absent or not a number.</summary>
    public static bool TryInt32(IReadOnlyDictionary<string, string> parameters, string name, out int value)
    {
        value = 0;
        if (!parameters.TryGetValue(name, out var text))
        {
            return false;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && Math.Abs(number) <= int.MaxValue)
        {
            value = (int)Math.Round(number);
            return true;
        }

        return false;
    }
}
