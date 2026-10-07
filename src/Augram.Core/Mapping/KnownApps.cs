using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// Well-known apps' executable names on Windows and macOS (F8, Joel 2026-10-07), for the best guess an app group makes
/// on a platform it has no names for: Chrome is "chrome.exe" on Windows and "Google Chrome" on macOS. Exact names,
/// compared case-insensitively. Apps whose Mac name carries a version ("Adobe Photoshop 2026") are left out rather
/// than guessed wrong. Grows as real use shows more; a guess is never stored, so a better table helps every group at once.
/// </summary>
public static class KnownApps
{
    private static readonly (string Windows, string Mac)[] Pairs =
    [
        ("chrome.exe", "Google Chrome"),
        ("msedge.exe", "Microsoft Edge"),
        ("firefox.exe", "firefox"),
        ("brave.exe", "Brave Browser"),
        ("opera.exe", "Opera"),
        ("vivaldi.exe", "Vivaldi"),
        ("explorer.exe", "Finder"),
        ("Code.exe", "Code"),
        ("sublime_text.exe", "Sublime Text"),
        ("EXCEL.EXE", "Microsoft Excel"),
        ("WINWORD.EXE", "Microsoft Word"),
        ("POWERPNT.EXE", "Microsoft PowerPoint"),
        ("OUTLOOK.EXE", "Microsoft Outlook"),
        ("Slack.exe", "Slack"),
        ("Discord.exe", "Discord"),
        ("Spotify.exe", "Spotify"),
        ("Notion.exe", "Notion"),
        ("Obsidian.exe", "Obsidian"),
        ("Figma.exe", "Figma"),
        ("gitkraken.exe", "GitKraken"),
        ("vlc.exe", "VLC"),
        ("Zoom.exe", "zoom.us"),
        ("blender.exe", "Blender"),
    ];

    /// <summary>The names <paramref name="names"/> (one platform's list) have on <paramref name="to"/>, in order, without duplicates; empty when none is known.</summary>
    public static IReadOnlyList<string> Guess(IEnumerable<string> names, HostPlatform to)
    {
        ArgumentNullException.ThrowIfNull(names);
        var guesses = new List<string>();
        foreach (var name in names)
        {
            var trimmed = name.Trim();
            foreach (var (windows, mac) in Pairs)
            {
                var (from, target) = to == HostPlatform.MacOS ? (windows, mac) : (mac, windows);
                if (string.Equals(from, trimmed, StringComparison.OrdinalIgnoreCase) && !guesses.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    guesses.Add(target);
                }
            }
        }

        return guesses;
    }
}
