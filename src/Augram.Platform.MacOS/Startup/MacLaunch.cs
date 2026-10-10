namespace Augram.Platform.MacOS.Startup;

/// <summary>What <see cref="MacLaunchEvent"/> found: whether this was a login-item launch, and the launch event's codes for the log ("aevt/oapp prdt=lgit", "none").</summary>
public sealed record MacLaunch(bool IsLoginItem, string Event);
