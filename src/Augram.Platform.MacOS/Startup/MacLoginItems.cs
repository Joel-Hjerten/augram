using System.Text;
using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Startup;

/// <summary>
/// The pure half of <see cref="MacLoginItemRegistration"/> and <see cref="MacLaunchEvent"/>, tested on every OS: what
/// <c>SMAppService</c>'s status means for Augram, and which launch Apple event is a login-item launch.
/// </summary>
internal static class MacLoginItems
{
    // SMAppServiceStatus (ServiceManagement, macOS 13+; an NSInteger, in this order in SMAppService.h and dotnet/macios).
    public const long NotRegistered = 0;
    public const long Enabled = 1;
    public const long RequiresApproval = 2;
    public const long NotFound = 3;

    // Apple event codes (CoreServices AE/AppleEvents.h, AERegistry.h).
    public const uint CoreEventClass = 0x61657674; // 'aevt' kCoreEventClass
    public const uint OpenApplication = 0x6F617070; // 'oapp' kAEOpenApplication
    public const uint PropData = 0x70726474; // 'prdt' keyAEPropData
    public const uint LaunchedAsLogInItem = 0x6C676974; // 'lgit' keyAELaunchedAsLogInItem

    /// <summary>
    /// Enabled → registered. Requires approval → the user has to allow it in System Settings › General › Login Items.
    /// Not found → removed there by the user: Apple documents it only as "couldn't find this service", but it is what
    /// <c>mainAppService</c> reports once the user took Augram out of Login Items (developer reports, 2024; Apple DTS advises
    /// against registering again behind the user's back), so Augram follows it instead of re-adding the item. Not
    /// registered, and anything unknown, → not registered.
    /// </summary>
    public static StartupStatus Map(long status) => status switch
    {
        Enabled => StartupStatus.Registered,
        RequiresApproval => StartupStatus.NeedsApproval,
        NotFound => StartupStatus.DisabledByUser,
        _ => StartupStatus.NotRegistered,
    };

    /// <summary>Whether <c>unregister</c> has anything to remove; for the rest it would only fail.</summary>
    public static bool CanUnregister(long status) => status is Enabled or RequiresApproval;

    /// <summary>
    /// A launch by the login-items machinery: the open-application event whose launch property says "as a login item". The
    /// test Electron's <c>electron_application_delegate.mm</c> makes for <c>wasOpenedAtLogin</c> (it checks the event id
    /// only; this also checks the class).
    /// </summary>
    public static bool IsLoginItemLaunch(uint eventClass, uint eventId, uint launchProperty) =>
        eventClass == CoreEventClass && eventId == OpenApplication && launchProperty == LaunchedAsLogInItem;

    /// <summary>"aevt/oapp prdt=lgit", for the log; a zero code reads "-".</summary>
    public static string Describe(uint eventClass, uint eventId, uint launchProperty) =>
        $"{Text(eventClass)}/{Text(eventId)} prdt={Text(launchProperty)}";

    /// <summary>The four characters of a <c>FourCharCode</c>, most significant byte first.</summary>
    public static string Text(uint code)
    {
        if (code == 0)
        {
            return "-";
        }

        Span<byte> bytes = [(byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code];
        return Encoding.ASCII.GetString(bytes);
    }

    public static uint Code(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length != 4)
        {
            throw new ArgumentException("A four-character code has four characters.", nameof(text));
        }

        return ((uint)text[0] << 24) | ((uint)text[1] << 16) | ((uint)text[2] << 8) | text[3];
    }
}
