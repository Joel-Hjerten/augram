namespace Augram.Core.Steps.Hdr;

/// <summary>What an <see cref="HdrStep"/> does to the display's HDR; names are stable because they end up in the config file.</summary>
public enum HdrAction
{
    /// <summary>On when it is off, off when it is on (what Joel's Win+Alt+B does).</summary>
    Toggle,

    On,

    Off,
}
