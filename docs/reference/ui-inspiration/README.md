# UI inspiration (plan 0006, the glass look)

Screenshots Joel collected on 2026-10-11 while deciding the look ([plan 0006](../../plans/0006-glass-look.md)). Each folder lists what Augram takes from it. The mockup that settled the decisions is [design/mockups/augram-glass-theme.html](../../../design/mockups/augram-glass-theme.html).

| Folder | What it shows | What Augram takes |
|---|---|---|
| `jetbrains/` | JetBrains' Islands theme, the default in their IDEs since 2025.3: rounded panels on a darker, tinted background, separated by gaps instead of lines; the selected tab a filled, rounded pill with a faint accent outline | Depth from layers and gaps, not outlines (decision 1); sub-tab pills (decision 5) |
| `mica/` | Microsoft's Mica pages: a base layer and a content layer, the card pattern, Mica vs Mica Alt in a tabbed title bar | The layer vocabulary; Chrome-style tabs in the title bar joined to their page (decision 5); Mica as "Wallpaper tint" (decision 2) |
| `sukiUI/` | SukiUI, an Avalonia theme library: one layout in dark and light with a switchable accent | Light and dark from the same tokens; the accent as a setting. Its cards are rounder and softer than Augram's |
| `raycast/` | Raycast on macOS and Windows: frosted window, translucent panels, headings on the glass above cards of rows, a theme picker with "Follow system appearance" | Translucent layers over glass; titles above the cards in Options (decision 12); Follow system. Raycast is much less glassy on Windows than on the Mac |

The Raycast images were AVIF and are now PNG (Joel converted them, 2026-10-11).
