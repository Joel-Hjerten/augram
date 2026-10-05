using System.Globalization;
using System.Text;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Diagnostics;

/// <summary>
/// The one text shape of a <see cref="LogEvent"/>, shared by the file sink and by the Diagnostics
/// tab's "copy last N lines" (N4):
/// <code>2026-10-05T21:14:03.123+02:00 INFO  hook      Hook installed {generation=2}</code>
/// Level padded to 5, source to 9, properties <c>key=value</c> space-separated in braces, an
/// exception on following lines indented by four spaces. Invariant culture throughout.
/// </summary>
public static class LogLineFormatter
{
    public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffzzz";
    public const string ExceptionIndent = "    ";
    private const int LevelWidth = 5;
    private const int SourceWidth = 9;

    public static string Format(LogEvent e)
    {
        var builder = new StringBuilder(160);
        Format(e, builder);
        return builder.ToString();
    }

    /// <summary>Appends the event without a trailing newline. Exception lines inside use <see cref="Environment.NewLine"/>.</summary>
    public static void Format(LogEvent e, StringBuilder into)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(into);

        into.Append(e.Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
        into.Append(' ');
        AppendPadded(into, Label(e.Level), LevelWidth);
        into.Append(' ');
        AppendPadded(into, e.Source, SourceWidth);
        into.Append(' ').Append(e.Message);

        if (e.Properties is { Count: > 0 } properties)
        {
            into.Append(" {");
            for (var i = 0; i < properties.Count; i++)
            {
                if (i > 0)
                {
                    into.Append(' ');
                }

                into.Append(properties[i].Key).Append('=');
                AppendValue(into, properties[i].Value);
            }

            into.Append('}');
        }

        if (e.Exception is not null)
        {
            foreach (var line in e.Exception.ToString().ReplaceLineEndings("\n").Split('\n'))
            {
                into.AppendLine().Append(ExceptionIndent).Append(line);
            }
        }
    }

    public static string Label(EventLevel level) => level switch
    {
        EventLevel.Trace => "TRACE",
        EventLevel.Debug => "DEBUG",
        EventLevel.Info => "INFO",
        EventLevel.Warning => "WARN",
        EventLevel.Error => "ERROR",
        _ => level.ToString().ToUpperInvariant(),
    };

    private static void AppendPadded(StringBuilder into, string text, int width)
    {
        into.Append(text);
        if (text.Length < width)
        {
            into.Append(' ', width - text.Length);
        }
    }

    private static void AppendValue(StringBuilder into, object? value)
    {
        switch (value)
        {
            case null:
                into.Append("null");
                break;
            case string s:
                into.Append(s);
                break;
            case bool b:
                into.Append(b ? "true" : "false");
                break;
            case IFormattable f:
                into.Append(f.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                into.Append(value);
                break;
        }
    }
}
