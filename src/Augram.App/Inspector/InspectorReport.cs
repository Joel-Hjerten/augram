using System.Text;
using Avalonia.Controls;

namespace Augram.App.Inspector;

/// <summary>The plain text F1 + click puts on the clipboard (ADR-0002 §5d): one line per item.</summary>
public static class InspectorReport
{
    public static string Describe(Control region, string theme)
    {
        ArgumentNullException.ThrowIfNull(region);
        var name = Region.GetName(region) ?? "(unnamed)";
        var info = Region.GetInfo(region);
        var text = new StringBuilder();
        text.Append("path: ").AppendLine(info?.Path ?? name);
        text.Append("component: ").Append(region.GetType().Name).Append(" \"").Append(name).AppendLine("\"");
        text.Append("kind: ").AppendLine(info?.Kind ?? "(none)");
        text.Append("source: ").AppendLine(info is { Source.Line: > 0 } ? info.Source.ToString() : "(none)");
        text.Append("binding: ").AppendLine(info?.BoundProperty ?? "(none)");
        text.Append("theme: ").Append(theme);
        return text.ToString();
    }
}
