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
/// sink, level/source filter selections, the search text (plan 0009, UI-only) and the two actions. Filtering itself runs
/// in <c>ItemList</c> through <see cref="MatchesLevel"/>, <see cref="MatchesSource"/> and <c>SearchQuery</c> over <see cref="SearchableText"/>.
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

    /// <summary>"All", then every source the solution emits (<see cref="LogSources.All"/>, alphabetical).</summary>
    public static IReadOnlyList<string> Sources { get; } = [All, .. LogSources.All];

    public ListSource<LogEvent> Source { get; }

    public string LevelFilter { get; set => SetProperty(ref field, value); } = All;

    public string SourceFilter { get; set => SetProperty(ref field, value); } = All;

    /// <summary>The search box's text (plan 0009): never persisted; cleared when the main window hides.</summary>
    public string SearchText { get; set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>What a line is searched by: its source and its message with the properties.</summary>
    public static string SearchableText(object row) => row is LogEvent e ? e.Source + " " + Detail(e) : string.Empty;

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
