using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Startup;

/// <summary>
/// Start at login on macOS (F7, 2026-10-10; <b>compiled, not yet run on a Mac</b>): Augram.app itself as a login item,
/// <c>[SMAppService mainAppService]</c> (ServiceManagement, macOS 13+; the framework is loaded here so the class exists).
/// <see cref="Status"/> is its <c>status</c> as <see cref="MacLoginItems.Map"/> reads it; <see cref="Set"/> calls
/// <c>registerAndReturnError:</c> or <c>unregisterAndReturnError:</c> (the latter only when something is registered) and turns
/// a refusal into a <see cref="StartupRegistrationException"/> with the <c>NSError</c>'s text, which the App logs. Such a
/// login item takes no arguments, so a login launch is detected rather than asked for (<see cref="MacLaunchEvent"/>).
/// Only an app bundle can be a login item: from a development build run as <c>dotnet Augram.App.dll</c> the status reads
/// not registered and turning it on fails with a reason (the App never asks a development build anyway). Main thread (the
/// App calls it from the UI thread); each call runs in its own autorelease pool.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacLoginItemRegistration : IStartupRegistration
{
    private const string ServiceManagementLibrary = "/System/Library/Frameworks/ServiceManagement.framework/ServiceManagement";

    private static readonly Lazy<nint> ServiceClass = new(LoadServiceClass);

    private readonly bool _bundled;

    public MacLoginItemRegistration()
        : this(Environment.ProcessPath)
    {
    }

    /// <param name="processPath">This process's executable; only one inside <c>Augram.app/Contents/MacOS/</c> can be a login item.</param>
    internal MacLoginItemRegistration(string? processPath)
    {
        _bundled = MacDock.IsBundled(processPath);
    }

    public StartupStatus Status
    {
        get
        {
            using var pool = ObjC.Pool();
            return MacLoginItems.Map(RawStatus(MainAppService()));
        }
    }

    public void Set(bool enabled)
    {
        using var pool = ObjC.Pool();
        var service = MainAppService();
        if (service == 0)
        {
            if (!enabled)
            {
                return;
            }

            throw new StartupRegistrationException(_bundled
                ? "Login items need macOS 13 or later (SMAppService is missing)."
                : "Only Augram.app can be a login item; this process is not an app bundle.");
        }

        if (!enabled && !MacLoginItems.CanUnregister(RawStatus(service)))
        {
            return;
        }

        var selector = enabled ? "registerAndReturnError:" : "unregisterAndReturnError:";
        if (MacNative.SendBoolWithError(service, ObjC.Selector(selector), out var error) == 0)
        {
            throw new StartupRegistrationException($"SMAppService {(enabled ? "register" : "unregister")} failed: {Describe(error)}");
        }
    }

    /// <summary>The main app's service; 0 outside a bundle or without ServiceManagement.</summary>
    private nint MainAppService() =>
        _bundled && ServiceClass.Value != 0 ? MacNative.SendPtr(ServiceClass.Value, ObjC.Selector("mainAppService")) : 0;

    private static long RawStatus(nint service) =>
        service == 0 ? MacLoginItems.NotRegistered : MacNative.SendNInt(service, ObjC.Selector("status"));

    /// <summary>"Operation not permitted (SMAppServiceErrorDomain 1)"; the text of an autoreleased <c>NSError</c>.</summary>
    private static string Describe(nint error)
    {
        if (error == 0)
        {
            return "no reason given";
        }

        var text = Cf.ReadString(MacNative.SendPtr(error, ObjC.Selector("localizedDescription"))) ?? "unknown error";
        var domain = Cf.ReadString(MacNative.SendPtr(error, ObjC.Selector("domain"))) ?? "?";
        var code = MacNative.SendNInt(error, ObjC.Selector("code"));
        return $"{text} ({domain} {code})";
    }

    private static nint LoadServiceClass() =>
        NativeLibrary.TryLoad(ServiceManagementLibrary, out _) ? ObjC.Class("SMAppService") : 0;
}
