// Augram spike 2 (plan 0001, M0 step 3): one throwaway program, one command-line switch per
// B-risk from requirements §6 part B. See README.md in this folder for how to run and read it.
//
//   dotnet run --project src/Augram.Spike2 -- activate   B1 foreground activation from a worker thread
//   dotnet run --project src/Augram.Spike2 -- overlay    B2 Avalonia transparent click-through trail overlay
//   dotnet run --project src/Augram.Spike2 -- hook       B3 hook resilience across sleep / lock
//   dotnet run --project src/Augram.Spike2 -- keys       B4 keyboard suppression, media keys, text entry
using System.Reflection;
using Augram.Spike2;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("usage: Augram.Spike2 <activate|overlay|hook|keys> [options]");
    Console.WriteLine("  overlay options: keep     keep the overlay window shown (empty) when idle instead of hiding it");
    Console.WriteLine("                   layered  also set WS_EX_LAYERED on the Avalonia window");
    Console.WriteLine("                   selftest draw three synthetic strokes via SharpHook simulation, then exit");
    Console.WriteLine("  keys options:    a|b|c|ab|bc|...  run only those parts (default abc)");
    return 2;
}

var mode = args[0].ToLowerInvariant();
Log.Init(mode);
Log.Info($"pid {Environment.ProcessId}, .NET {Environment.Version}, SharpHook {Version(typeof(SharpHook.SimpleGlobalHook))}, " +
         $"Avalonia {Version(typeof(Avalonia.Application))}, log file {Log.FilePath}");

// Not disposed on purpose: the ProcessExit handler below may run after Main has returned.
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Log.Info("Ctrl+C: shutting down");
    cts.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

try
{
    return mode switch
    {
        "activate" => ActivateMode.Run(cts.Token),
        "hook" => HookMode.Run(cts.Token),
        "keys" => KeysMode.Run(args, cts.Token),
        "overlay" => OverlayMode.Run(args, cts),
        _ => Unknown(mode),
    };
}
finally
{
    Log.Info("exited cleanly");
    Log.Close();
}

static int Unknown(string mode)
{
    Log.Info($"unknown mode '{mode}'");
    return 2;
}

static string Version(Type t) =>
    t.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
    ?? t.Assembly.GetName().Version?.ToString() ?? "?";
