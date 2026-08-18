namespace Jumoo.TranslationManager.Automate.Actions.Models;

/// <summary>A capped-array-friendly summary of one translation job created by a run.</summary>
public sealed class TranslationJobSummary
{
    public int Id { get; init; }
    public Guid Key { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid SetKey { get; init; }
    public string SourceCulture { get; init; } = string.Empty;
    public string TargetCulture { get; init; } = string.Empty;
    public int NodeCount { get; init; }

    /// <summary>Mapped via <c>TranslationStatusMapper</c> - never a raw JobStatus sentinel.</summary>
    public string Status { get; init; } = string.Empty;
}
