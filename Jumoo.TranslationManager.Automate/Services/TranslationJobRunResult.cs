using Jumoo.TranslationManager.Automate.Actions.Models;

namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>Result of <see cref="TranslationRunner.CheckJobAsync"/> / <see cref="TranslationRunner.ApproveJobAsync"/>.</summary>
public sealed class TranslationJobRunResult
{
    public required TranslationRunStatus Status { get; init; }
    public TranslationJobOutput? Output { get; init; }
    public string? Reason { get; init; }
    public Exception? Exception { get; init; }

    public static TranslationJobRunResult Ok(TranslationJobOutput output)
        => new() { Status = TranslationRunStatus.Success, Output = output };

    public static TranslationJobRunResult Invalid(string reason)
        => new() { Status = TranslationRunStatus.Validation, Reason = reason };

    public static TranslationJobRunResult Fail(string reason, Exception? ex = null)
        => new() { Status = TranslationRunStatus.Failed, Reason = reason, Exception = ex };
}
