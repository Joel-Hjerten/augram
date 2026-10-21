namespace Augram.Engine.Input;

/// <summary>
/// What <see cref="SharpHookInputSource"/> makes of a simulated mouse event (<c>IsEventSimulated</c>) that <see cref="OwnInjections"/>
/// does not claim as Augram's own: another process posted it. Chosen once by the composition root (<c>App/Hosting/Modules/EngineModule</c>).
/// </summary>
public enum ForeignInput
{
    /// <summary>
    /// The Windows rule until 0.20.5, no longer picked by the app: a press or move is dropped (other utilities' synthetic input is
    /// ignored, learnings 0001) and a release is taken as released elsewhere (plan 0005 decision 10: Eyeris's loupe chord swallows
    /// the real release and posts its own).
    /// </summary>
    Drop,

    /// <summary>
    /// Both platforms (macOS since 0.20.4, Windows since 0.20.5): a press, release or move is taken as real input. The Wacom driver
    /// posts the pen's tip, buttons and moves (on Windows in pen mode with Windows Ink on), and both hooks mark posted input as
    /// simulated, so dropping it left the pen outside Augram (learnings 0006, "Wacom on the Mac" and its Windows paragraph).
    /// Augram's own, announced and claimed, are still dropped.
    /// </summary>
    Take,
}
