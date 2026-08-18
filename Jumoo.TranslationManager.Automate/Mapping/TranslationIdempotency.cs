using Jumoo.TranslationManager.Core.Models;

namespace Jumoo.TranslationManager.Automate.Mapping;

/// <summary>
/// Idempotency keys for this package's job triggers. Translation Manager's job notifications
/// carry no CMS version id, so Automate's built-in <c>GenerateIdempotencyKey(entityKey,
/// versionId)</c> helper (designed for content/media events) does not apply - these are hand
/// built instead.
/// </summary>
internal static class TranslationIdempotency
{
    /// <summary>
    /// For submitted/received events: the timestamp TM stamps on the job for that transition
    /// separates a genuine re-run after a status reset from a duplicate notification for the
    /// same event.
    /// </summary>
    public static string ForJobEvent(string alias, TranslationJob job, DateTime? stamp)
        => $"{alias}:{job.Key}:s{(int)job.Status}:t{stamp?.Ticks ?? 0}";

    /// <summary>
    /// For approved/published events: TM stamps no timestamp on approval, but the count of
    /// approved nodes is monotonic within one job, so it distinguishes a partial approval from
    /// a full one while still collapsing a duplicate notification for the same approval - which
    /// is the desired behaviour, not a bug: repeat-approving an unchanged job should dedupe.
    /// </summary>
    public static string ForJobProgress(string alias, TranslationJob job, int approvedNodeCount)
        => $"{alias}:{job.Key}:s{(int)job.Status}:n{approvedNodeCount}";
}
