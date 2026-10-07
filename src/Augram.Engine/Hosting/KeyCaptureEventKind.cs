namespace Augram.Engine.Hosting;

/// <summary>What a <see cref="KeyCaptureEvent"/> reports.</summary>
public enum KeyCaptureEventKind
{
    /// <summary>A physical key went down (or repeated) while the capture was armed.</summary>
    KeyDown,

    /// <summary>A physical key went up while the capture was armed.</summary>
    KeyUp,

    /// <summary>The engine ended the capture on its own (idle watchdog, another capture armed, hook reset, engine stopped); the last event the callback gets.</summary>
    Released,
}
