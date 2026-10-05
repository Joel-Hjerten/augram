using System.Diagnostics;
using System.Globalization;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The Diagnostics › Log tail (N4) over <see cref="InMemorySink"/>: a list source that follows the
/// sink, level/source filter selections, and the two actions. Filtering itself runs in <c>ItemList</c>
/// through <see cref="MatchesLevel"/> and <see cref="MatchesSource"/>.
/// </summary>
public sealed class LogViewModel : ObservableObject
{
    public const string All = "All";
    public const int CopyLineCount = 200;

    private readonly InMemorySink _sink;
    private readonly string _logsFolder;
    private readonly IClipboardText _clipboard;

    public LogViewModel(InMemorySink sink, string logsFolder, IClipboardText clipboard)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentException.ThrowIfNullOrWhiteSpace(logsFolder);
        ArgumentNullException.ThrowIfNull(clipboard);
        _sink = sink;
        _logsFolder = logsFolder;
        _clipboard = clipboard;
        Source = new ListSource<LogEvent>(sink.Snapshot);
        sink.Changed += (_, _) => Source.NotifyChanged();
    }

    public static IReadOnlyList<string> Levels { get; } = [All, .. Enum.GetNames<EventLevel>()];

    /// <summary>The N4 source names, plus the app's own; a source not listed here still shows under "All".</summary>
    public static IReadOnlyList<string> Sources { get; } =
        [All, "app", "hook", "capture", "recognition", "activate", "steps", "config", "overlay", ChannelEventLog.Source];

    public ListSource<LogEvent> Source { get; }

    public string LevelFilter { get; set => SetProperty(ref field, value); } = All;

    public string SourceFilter { get; set => SetProperty(ref field, value); } = All;

    public static bool MatchesLevel(object row, string selected) =>
        selected == All || (row is LogEvent e && Enum.TryParse<EventLevel>(selected, out var minimum) && e.Level >= minimum);

    public static bool MatchesSource(object row, string selected) =>
        selected == All || (row is LogEvent e && string.Equals(e.Source, selected, StringComparison.Ordinal));

    public static string Time(LogEvent e) => e.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public static string Detail(LogEvent e)
    {
        var line = LogLineFormatter.Format(e);
        var brace = line.IndexOf(" {", StringComparison.Ordinal);
        return brace < 0 ? e.Message : e.Message + line[brace..];
    }

    /// <summary>The last <see cref="CopyLineCount"/> events in file-line format, for pasting at an agent.</summary>
    public string LastLines()
    {
        var all = _sink.Snapshot();
        var start = Math.Max(0, all.Count - CopyLineCount);
        return string.Join(Environment.NewLine, all.Skip(start).Select(LogLineFormatter.Format));
    }

    public Task CopyLastLinesAsync() => _clipboard.SetTextAsync(LastLines());

    public void OpenLogFolder()
    {
        Directory.CreateDirectory(_logsFolder);
        Process.Start(new ProcessStartInfo(_logsFolder) { UseShellExecute = true });
    }
}
