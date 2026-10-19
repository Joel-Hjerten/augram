using Augram.Core.Capture;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// Where <see cref="MacRemapButtonSimulator"/> announces each mouse event just before it posts it, so the hook knows it for Augram's
/// own: a button's press, a button's release, or a move or drag (one slot). Platform may not reference the Engine, so the engine's
/// ledger (<c>OwnInjections</c>) is reached through these delegates, wired in <c>EngineModule.CreateSimulator</c>.
/// </summary>
public sealed record MacPostedInput(Action<MouseButton> Press, Action<MouseButton> Release, Action Move);
