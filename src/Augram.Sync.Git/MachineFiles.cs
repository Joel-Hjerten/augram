using System.Text;

namespace Augram.Sync.Git;

/// <summary>
/// The clone's <c>machines/</c> folder: one <c>&lt;machine-id&gt;.json</c> per machine (F8 sync). The id
/// becomes a file name on every OS, so it is ASCII letters, digits and '-' only, never a reserved
/// Windows device name. Files are written UTF-8 without BOM with <c>\n</c> line endings, so the same
/// content always gives the same blob and an unchanged publish commits nothing.
/// </summary>
internal static class MachineFiles
{
    public const string FolderName = "machines";
    public const int MaxIdLength = 64;

    private const string Extension = ".json";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly HashSet<string> ReservedNames = new(
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsValidId(string? machineId) =>
        !string.IsNullOrEmpty(machineId)
        && machineId.Length <= MaxIdLength
        && machineId.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
        && !ReservedNames.Contains(machineId);

    /// <summary>The file's path inside the clone as git spells it (forward slash on every OS).</summary>
    public static string RelativePath(string machineId) => FolderName + "/" + machineId + Extension;

    public static void Write(string clone, string machineId, string content)
    {
        var folder = Path.Combine(clone, FolderName);
        Directory.CreateDirectory(folder);
        var text = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        File.WriteAllText(Path.Combine(folder, machineId + Extension), text, Utf8NoBom);
    }

    /// <summary>Every <c>machines/*.json</c> as file name without extension → text. Unreadable files are skipped.</summary>
    public static IReadOnlyDictionary<string, string> ReadAll(string clone)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var folder = Path.Combine(clone, FolderName);
        try
        {
            if (!Directory.Exists(folder))
            {
                return files;
            }

            foreach (var path in Directory.EnumerateFiles(folder))
            {
                if (string.Equals(Path.GetExtension(path), Extension, StringComparison.Ordinal) && TryRead(path, out var text))
                {
                    files[Path.GetFileNameWithoutExtension(path)] = text;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The folder went away or is unreadable: report what was read so far.
        }

        return files;
    }

    private static bool TryRead(string path, out string text)
    {
        try
        {
            text = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            text = string.Empty;
            return false;
        }
    }
}
