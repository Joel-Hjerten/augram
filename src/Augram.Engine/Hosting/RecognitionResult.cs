using Augram.Core.Diagnostics;

namespace Augram.Engine.Hosting;

/// <summary>
/// What <see cref="StrokeRecognizer.Recognize"/> hands the worker: the event for the App and, for a
/// recognised gesture, the recognition log entry as a draft, not yet added. Whoever decides the
/// stroke's fate (the executor after resolving a command, or the worker when the stroke was consumed
/// or no mapping is wired) completes the group, command or reason and adds it. A no-match entry is
/// final at recognition and is added by the recognizer, so <see cref="Draft"/> is null for it.
/// </summary>
internal sealed record RecognitionResult(EngineEvent Event, RecognitionLogEntry? Draft);
