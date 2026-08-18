namespace Jumoo.TranslationManager.Automate.Actions.Models;

/// <summary>Output shared by the <c>checkJob</c> and <c>approveJob</c> actions.</summary>
public sealed class TranslationJobOutput
{
    public int Id { get; init; }
    public Guid Key { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid SetKey { get; init; }
    public string SourceCulture { get; init; } = string.Empty;
    public string TargetCulture { get; init; } = string.Empty;

    /// <summary>Mapped via <c>TranslationStatusMapper</c> - never a raw JobStatus sentinel.</summary>
    public string Status { get; init; } = string.Empty;

    public bool StatusChanged { get; init; }
    public bool IsComplete { get; init; }
    public bool IsError { get; init; }
    public int NodeCount { get; init; }

    /// <summary>Only meaningful once the job's nodes have been loaded - 0 otherwise.</summary>
    public int ApprovedNodeCount { get; init; }

    public Guid ProviderKey { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string? ProviderStatus { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Submitted { get; init; }
    public DateTime? Received { get; init; }
    public bool Approved { get; init; }
    public bool Published { get; init; }
}
