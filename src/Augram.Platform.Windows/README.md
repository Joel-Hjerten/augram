# Augram.Platform.Windows

Windows implementations of the Core ports that need the OS: `IWindowSystem` (window under point, process identity, foreground root), `IWindowOperations`, `IProcessLauncher` where Engine cannot, and the layered-window overlay fallback if the Avalonia overlay fails spike 2 (B2).

**May reference:** `Augram.Core` only. Win32 goes through `P/Invoke` here and nowhere else; mark types `[SupportedOSPlatform("windows")]`.

**Must never contain:** Avalonia, SharpHook, business rules, or anything Core could do without the OS. Nothing above the ports may know this project exists except the composition root.

Lands in M1 step 5. Empty for M0.
