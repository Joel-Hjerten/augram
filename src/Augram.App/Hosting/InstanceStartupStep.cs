namespace Augram.App.Hosting;

/// <summary>What <see cref="InstanceStartup.Begin"/> found.</summary>
public enum InstanceStartupStep
{
    /// <summary>This launch holds the single-instance name: start.</summary>
    Run,

    /// <summary>A different build is running: ask the user once Avalonia runs (<see cref="InstanceStartup.ChooseAsync"/>).</summary>
    Choose,

    /// <summary>The running Augram was shown (or is stuck): this launch ends here.</summary>
    Exit,
}
