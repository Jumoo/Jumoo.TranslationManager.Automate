using Jumoo.TranslationManager.Core.Models;

namespace Jumoo.TranslationManager.Automate.Mapping;

/// <summary>
/// Maps Translation Manager's <see cref="JobStatus"/> to the strings and flags this package
/// surfaces on outputs. TM's enum mixes real statuses with comparison-threshold sentinels
/// (MAX_SUBMITTED, STATUSMAX, ARCHIVE_MAX) that must never reach a workflow's output - every
/// status this package emits goes through here rather than a raw <c>.ToString()</c>.
/// </summary>
public static class TranslationStatusMapper
{
    public static string ToName(JobStatus status) => status switch
    {
        JobStatus.Created => "Created",
        JobStatus.Submitted => "Submitted",
        JobStatus.Returned => "Returned",
        JobStatus.Partial => "Partial",
        JobStatus.Received => "Received",
        JobStatus.PartialApproved => "PartialApproved",
        JobStatus.Processing => "Processing",
        JobStatus.Reviewing => "Reviewing",
        JobStatus.Accepted => "Accepted",
        JobStatus.Closed => "Closed",
        JobStatus.Error => "Error",
        _ => "Unknown",
    };

    /// <summary>True once a job has come back from the connector and is ready to review/approve.</summary>
    public static bool IsComplete(JobStatus status) => status
        is JobStatus.Received or JobStatus.PartialApproved or JobStatus.Accepted or JobStatus.Closed;

    public static bool IsError(JobStatus status) => status is JobStatus.Error;
}
