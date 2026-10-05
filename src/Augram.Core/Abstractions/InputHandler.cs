namespace Augram.Core.Abstractions;

/// <summary>
/// Receives one <see cref="RawInput"/> on the input source's own thread and returns whether the
/// OS and the app under the pointer must never see it (true = suppress). Must be O(1): the hook
/// thread is waiting on the answer (CLAUDE.md invariant 1), so append, decide, enqueue, return.
/// </summary>
public delegate bool InputHandler(in RawInput input);
