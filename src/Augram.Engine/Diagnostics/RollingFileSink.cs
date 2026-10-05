using System.Globalization;
using System.Text;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Diagnostics;

/// <summary>
/// One UTF-8 file per day, <c>augram-yyyyMMdd.log</c>, in <see cref="DirectoryPath"/> (N4: <c>logs/</c>
/// beside the config). The file is chosen by each event's own timestamp date, so a backlog drained after
/// midnight still lands in the right day. Files dated more than <see cref="RetainDays"/> days before the
/// injected clock's today are deleted at construction and at every rotation. Writes are buffered; a timer
/// flushes every <see cref="FlushInterval"/>, the channel flushes when its queue empties, and
/// <see cref="Dispose"/> flushes and closes. The file is opened with read-write sharing so an editor or
/// <c>tail</c> can follow it while the app runs.
/// </summary>
public sealed class RollingFileSink : ILogSink
{
    public const string FilePrefix = "augram-";
    public const string FileSuffix = ".log";
    public const int DefaultRetainDays = 7;
    public static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(500);
    private const string DateFormat = "yyyyMMdd";

    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private readonly StringBuilder _line = new(256);
    private readonly Timer _flushTimer;
    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private bool _dirty;
    private bool _disposed;

    public RollingFileSink(string directory, Func<DateTimeOffset>? clock = null, int retainDays = DefaultRetainDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegative(retainDays);
        DirectoryPath = Path.GetFullPath(directory);
        RetainDays = retainDays;
        _clock = clock ?? (() => DateTimeOffset.Now);
        Directory.CreateDirectory(DirectoryPath);
        DeleteExpired();
        _flushTimer = new Timer(_ => Flush(), null, FlushInterval, FlushInterval);
    }

    public string DirectoryPath { get; }

    public int RetainDays { get; }

    /// <summary>The file the last event went to; null before the first write.</summary>
    public string? CurrentFilePath { get; private set; }

    public static string FileNameFor(DateOnly date) => FilePrefix + date.ToString(DateFormat, CultureInfo.InvariantCulture) + FileSuffix;

    public static bool TryParseFileDate(string fileName, out DateOnly date)
    {
        date = default;
        if (fileName.Length != FilePrefix.Length + DateFormat.Length + FileSuffix.Length
            || !fileName.StartsWith(FilePrefix, StringComparison.Ordinal)
            || !fileName.EndsWith(FileSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = fileName.AsSpan(FilePrefix.Length, DateFormat.Length);
        return DateOnly.TryParseExact(digits, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public void Write(LogEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var date = DateOnly.FromDateTime(e.Timestamp.DateTime);
            if (_writer is null || date != _writerDate)
            {
                Rotate(date);
            }

            LogLineFormatter.Format(e, _line);
            _writer!.WriteLine(_line);
            _line.Clear();
            _dirty = true;
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            if (!_dirty || _writer is null)
            {
                return;
            }

            try
            {
                _writer.Flush();
                _dirty = false;
            }
            catch (IOException)
            {
                // Disk full or file gone: keep the buffer, try again on the next flush. The timer thread must not throw.
            }
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Rotate(DateOnly date)
    {
        _writer?.Dispose();
        CurrentFilePath = Path.Combine(DirectoryPath, FileNameFor(date));
        var stream = new FileStream(CurrentFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _writerDate = date;
        _dirty = false;
        DeleteExpired();
    }

    private void DeleteExpired()
    {
        var today = DateOnly.FromDateTime(_clock().DateTime);
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, FilePrefix + "*" + FileSuffix))
        {
            if (!TryParseFileDate(Path.GetFileName(path), out var date) || today.DayNumber - date.DayNumber <= RetainDays)
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Held open by another process (a viewer): try again at the next rotation.
            }
            catch (UnauthorizedAccessException)
            {
                // Same.
            }
        }
    }
}
