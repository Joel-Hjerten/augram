using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>An explicit exception to a one-home guard: a repository-relative path (a file, or a folder ending in '/'), why it may, and optionally the line text it may only appear on.</summary>
internal sealed record Allowed(string Path, string Reason, string? OnLine = null)
{
    public bool Covers(SourceHit hit)
        => (Path.EndsWith('/') ? hit.File.StartsWith(Path, StringComparison.Ordinal) : hit.File == Path)
            && (OnLine is null || hit.Text.Contains(OnLine, StringComparison.Ordinal));
}

/// <summary>One match of a guard's pattern: repository-relative path with forward slashes, 1-based line, the code line.</summary>
internal sealed record SourceHit(string File, int Line, string Text)
{
    public override string ToString() => $"{File}:{Line}: {Text.Trim()}";
}

/// <summary>
/// The source-scanning half of the one-home guards (docs/reference/one-home-guards.md): reads the repository's .cs and .axaml
/// files with comments removed (string literals kept, line numbers kept), finds a pattern, and fails with the one home to use
/// and the allowlist. An allowlist entry that no longer matches anything fails too, so the lists stay short and true.
/// </summary>
internal static class SourceScan
{
    private static readonly Dictionary<string, string[]> Cache = [];

    /// <summary>Every .cs file under <paramref name="folders"/> (repository-relative, forward slashes), build output left out.</summary>
    public static IEnumerable<string> Code(params string[] folders) => Files("*.cs", folders);

    /// <summary>Every .axaml file under <paramref name="folders"/>.</summary>
    public static IEnumerable<string> Markup(params string[] folders) => Files("*.axaml", folders);

    /// <summary>The lines of <paramref name="file"/> (repository-relative) with comments blanked out.</summary>
    public static string[] Lines(string file)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(file, out var lines))
            {
                var text = File.ReadAllText(Path.Combine(RepositoryPaths.Root, file));
                lines = (file.EndsWith(".axaml", StringComparison.Ordinal) ? StripXmlComments(text) : StripCodeComments(text)).Split('\n');
                Cache[file] = lines;
            }

            return lines;
        }
    }

    /// <summary>
    /// Every match of <paramref name="pattern"/> in <paramref name="files"/>, one hit per line it starts on. The pattern runs over
    /// the whole file, so it may span lines ("a loop, then a key press within three lines").
    /// </summary>
    public static List<SourceHit> Find(Regex pattern, IEnumerable<string> files)
    {
        var hits = new List<SourceHit>();
        foreach (var file in files)
        {
            var lines = Lines(file);
            var text = string.Join('\n', lines);
            var lastLine = -1;
            foreach (Match match in pattern.Matches(text))
            {
                var line = text.AsSpan(0, match.Index).Count('\n');
                if (line != lastLine)
                {
                    hits.Add(new SourceHit(file, line + 1, lines[line].TrimEnd('\r')));
                    lastLine = line;
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// Fails unless every hit is covered by an <see cref="Allowed"/> entry and every entry covers a hit. The message says what was
    /// found, <paramref name="useInstead"/> (the one home), and the allowlist with its reasons.
    /// </summary>
    public static void AssertOnlyAllowed(IReadOnlyList<SourceHit> hits, string useInstead, params Allowed[] allowed)
    {
        var offenders = hits.Where(hit => !allowed.Any(entry => entry.Covers(hit))).ToList();
        var stale = allowed.Where(entry => !hits.Any(entry.Covers)).ToList();
        if (offenders.Count == 0 && stale.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        if (offenders.Count > 0)
        {
            message.AppendLine("A second implementation of a job that has one home:");
            offenders.ForEach(hit => message.AppendLine("  " + hit));
            message.AppendLine(useInstead);
        }

        if (stale.Count > 0)
        {
            message.AppendLine("Allowlist entries that match nothing any more (remove them):");
            stale.ForEach(entry => message.AppendLine($"  {entry.Path}{(entry.OnLine is null ? string.Empty : $" [{entry.OnLine}]")}"));
        }

        if (allowed.Length > 0)
        {
            message.AppendLine("Allowlisted (docs/reference/one-home-guards.md):");
            foreach (var entry in allowed)
            {
                message.AppendLine($"  {entry.Path}{(entry.OnLine is null ? string.Empty : $" [{entry.OnLine}]")}: {entry.Reason}");
            }
        }

        Assert.Fail(message.ToString());
    }

    /// <summary>Find + <see cref="AssertOnlyAllowed"/> in one call.</summary>
    public static void AssertOnlyIn(Regex pattern, IEnumerable<string> files, string useInstead, params Allowed[] allowed)
        => AssertOnlyAllowed(Find(pattern, files), useInstead, allowed);

    private static IEnumerable<string> Files(string searchPattern, string[] folders)
    {
        foreach (var folder in folders)
        {
            var full = Path.Combine(RepositoryPaths.Root, folder);
            if (File.Exists(full))
            {
                yield return folder;
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(full, searchPattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
                if (!relative.Contains("/bin/", StringComparison.Ordinal) && !relative.Contains("/obj/", StringComparison.Ordinal))
                {
                    yield return relative;
                }
            }
        }
    }

    /// <summary>
    /// Blanks // and /* */ comments (doc comments included) and keeps everything else: string and char literals (regular, verbatim,
    /// interpolated, raw) pass through untouched, and every newline stays, so line numbers hold.
    /// </summary>
    internal static string StripCodeComments(string text)
    {
        var output = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && next == '*')
            {
                i += 2;
                while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                {
                    if (text[i] == '\n')
                    {
                        output.Append('\n');
                    }

                    i++;
                }

                i += 2;
            }
            else if (c == '"' || ((c == '@' || c == '$') && (next == '"' || next is '@' or '$')))
            {
                i = CopyString(text, i, output);
            }
            else if (c == '\'')
            {
                var end = i + 1;
                while (end < text.Length && text[end] != '\'' && text[end] != '\n')
                {
                    end += text[end] == '\\' ? 2 : 1;
                }

                end = Math.Min(end + 1, text.Length);
                output.Append(text, i, end - i);
                i = end;
            }
            else
            {
                output.Append(c);
                i++;
            }
        }

        return output.ToString();
    }

    private static int CopyString(string text, int start, StringBuilder output)
    {
        var i = start;
        var verbatim = false;
        while (text[i] is '@' or '$')
        {
            verbatim |= text[i] == '@';
            i++;
        }

        var quotes = 0;
        while (i + quotes < text.Length && text[i + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            var close = text.IndexOf(new string('"', quotes), i + quotes, StringComparison.Ordinal);
            var end = close < 0 ? text.Length : close + quotes;
            output.Append(text, start, end - start);
            return end;
        }

        i++;
        while (i < text.Length)
        {
            if (verbatim && text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"')
            {
                i += 2;
            }
            else if (!verbatim && text[i] == '\\')
            {
                i += 2;
            }
            else if (text[i] == '"' || (!verbatim && text[i] == '\n'))
            {
                i++;
                break;
            }
            else
            {
                i++;
            }
        }

        i = Math.Min(i, text.Length);
        output.Append(text, start, i - start);
        return i;
    }

    private static string StripXmlComments(string text)
        => Regex.Replace(text, "<!--.*?-->", match => new string('\n', match.Value.Count(ch => ch == '\n')), RegexOptions.Singleline);
}
