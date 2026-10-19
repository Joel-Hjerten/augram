namespace Augram.Engine.Input;

/// <summary>
/// What <see cref="SharpHookInputSource"/> makes of a simulated mouse event (<c>IsEventSimulated</c>) that <see cref="OwnInjections"/>
/// does not claim as Augram's own: another process posted it. Chosen once by the composition root (<c>App/Hosting/Modules/EngineModule</c>).
/// </summary>
public enum ForeignInput
{
    /// <summary>
    /// Windows: a press or move is dropped (other utilities' synthetic input is ignored, learnings 0001) and a release is taken as
    /// released elsewhere (plan 0005 decision 10: Eyeris's loupe chord swallows the real release and posts its own).
    /// </summary>
    Drop,

    /// <summary>
    /// macOS (2026-10-19): a press, release or move is taken as real input. libuiohook marks every event with a posting process id
    /// as simulated, which includes everything the Wacom driver posts for the pen (its tip, buttons and moves), so dropping it left the
    /// pen outside Augram (learnings 0006, "Wacom on the Mac"). Augram's own, announced and claimed, are still dropped.
    /// </summary>
    Take,
}
