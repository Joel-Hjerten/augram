namespace Augram.Core.Steps.ClearClipboard;

/// <summary>Empties the system clipboard (SP.net's <c>clip.Clear()</c>). No parameters: every instance is the same step, and it means the same on every platform (F8).</summary>
public sealed record ClearClipboardStep : IStep
{
    public const string Text = "Clear clipboard";

    public IStepType Type => ClearClipboardStepType.Instance;

    public string Summary => Text;
}
