using Jumoo.TranslationManager.Automate.Dispatch;

namespace Jumoo.TranslationManager.Automate.Triggers.Jobs;

/// <summary>Output shared by the <c>jobSubmitted</c>/<c>jobReceived</c>/<c>jobApproved</c>/<c>jobPublished</c> triggers.</summary>
public sealed class TranslationJobTriggerOutput : ITranslationSetScopedOutput
{
    public int JobId { get; init; }
    public Guid JobKey { get; init; }
    public string JobName { get; init; } = string.Empty;
    public Guid SetKey { get; init; }
    public string SourceCulture { get; init; } = string.Empty;
    public string TargetCulture { get; init; } = string.Empty;
    public int NodeCount { get; init; }
    public int ApprovedNodeCount { get; init; }

    /// <summary>Mapped via <c>TranslationStatusMapper</c> - never a raw JobStatus sentinel.</summary>
    public string Status { get; init; } = string.Empty;

    public bool IsError { get; init; }
    public Guid ProviderKey { get; init; }
    public string? ProviderName { get; init; }
    public string? GroupId { get; init; }
    public Guid UserKey { get; init; }
    public bool Publish { get; init; }

    /// <summary>
    /// Master content keys for the job's translation nodes, so a workflow can
    /// <c>forEach</c> -&gt; <c>publishContent</c>. Best-effort: only populated for nodes
    /// already loaded on the job at notification time, and capped - a bulk job can span
    /// thousands of nodes and this array is persisted in the outbox.
    /// </summary>
    public Guid[] ContentKeys { get; init; } = [];

    Guid ITranslationSetScopedOutput.GetSetKey() => SetKey;
}
