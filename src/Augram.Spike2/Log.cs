// Console + file logger. Every line goes to stdout and to spike2-<mode>.log next to the exe
// so Joel can run a mode for hours and send the file back.
namespace Augram.Spike2;

internal static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;
    public static string? FilePath { get; private set; }

    public static void Init(string mode)
    {
        FilePath = Path.Combine(AppContext.BaseDirectory, $"spike2-{mode}.log");
        _file = new StreamWriter(FilePath, append: true) { AutoFlush = true };
        Info($"==== spike2 {mode} started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====");
    }

    public static void Info(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        lock (Gate)
        {
            Console.WriteLine(line);
            _file?.WriteLine(line);
        }
    }

    /// <summary>Instruction paragraph for Joel, visually separated from the log stream.</summary>
    public static void Instructions(string text)
    {
        lock (Gate)
        {
            Console.WriteLine();
            Console.WriteLine(new string('-', 78));
            Console.WriteLine(text);
            Console.WriteLine(new string('-', 78));
            Console.WriteLine();
            _file?.WriteLine("INSTRUCTIONS: " + text.ReplaceLineEndings(" "));
        }
    }

    public static void Close()
    {
        lock (Gate)
        {
            _file?.Flush();
            _file?.Dispose();
            _file = null;
        }
    }
}
