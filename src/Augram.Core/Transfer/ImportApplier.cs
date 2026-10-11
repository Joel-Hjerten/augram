using Augram.Core.Config;

namespace Augram.Core.Transfer;

/// <summary>
/// Applies an import on the stores' thread (plan 0003): the reviewed plan's result first; when a store moved since that plan
/// was made (a sync applied while the review was open) <see cref="ImportResult.ApplyTo"/> refuses it, and the import plans
/// again from the stores with the same choices, which are keyed by item and so carry over, at most <see cref="Attempts"/>
/// plans in all. The sync's own retry is <c>Sync/SyncStoreWriter</c>, with the same bound.
/// </summary>
public static class ImportApplier
{
    /// <summary>How many plans are tried, the reviewed one included, while the stores keep moving between planning and applying.</summary>
    public const int Attempts = 3;

    /// <param name="session">The stores, and the snapshots a new plan is made from.</param>
    /// <param name="plan">The plan the user reviewed.</param>
    /// <param name="choices">The user's choices.</param>
    /// <param name="options">What a new plan is made with: the reviewed plan's options.</param>
    public static ImportApplied Apply(ConfigSession session, ImportPlan plan, ImportChoices choices, ImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(options);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
            {
                plan = ImportPlan.Create(plan.File, session.Document, options);
            }

            var result = plan.Resolve(choices);
            if (result.ApplyTo(session.Settings, session.Gestures, session.Mapping))
            {
                return new ImportApplied(plan, result, attempt);
            }
        }

        return new ImportApplied(plan, null, Attempts - 1);
    }
}
