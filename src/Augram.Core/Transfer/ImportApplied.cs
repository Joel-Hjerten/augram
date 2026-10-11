namespace Augram.Core.Transfer;

/// <summary>
/// What <see cref="ImportApplier.Apply"/> did: the plan it applied, or last tried (the reviewed one, or one made again because
/// the stores moved), the result when it was applied (null when the stores kept moving and nothing changed), and how many
/// times it planned again.
/// </summary>
public sealed record ImportApplied(ImportPlan Plan, ImportResult? Result, int Replanned)
{
    public bool Applied => Result is not null;
}
