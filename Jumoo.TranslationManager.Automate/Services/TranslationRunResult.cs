using Jumoo.TranslationManager.Automate.Actions.Models;

namespace Jumoo.TranslationManager.Automate.Services;

public enum TranslationRunStatus
{
    Success,
    NothingToDo,
    Busy,
    Validation,
    ProviderUnavailable,
    Failed,
}

/// <summary>Result of <see cref="TranslationRunner.RunAsync"/>.</summary>
public sealed class TranslationRunResult
{
    public required TranslationRunStatus Status { get; init; }
    public TranslateContentOutput? Output { get; init; }
    public string? Reason { get; init; }
    public Exception? Exception { get; init; }

    public static TranslationRunResult Ok(TranslateContentOutput output)
        => new() { Status = TranslationRunStatus.Success, Output = output };

    public static TranslationRunResult Nothing(string reason)
        => new() { Status = TranslationRunStatus.NothingToDo, Reason = reason };

    public static TranslationRunResult Busy(string reason)
        => new() { Status = TranslationRunStatus.Busy, Reason = reason };

    public static TranslationRunResult Invalid(string reason)
        => new() { Status = TranslationRunStatus.Validation, Reason = reason };

    public static TranslationRunResult ProviderUnavailable(string reason)
        => new() { Status = TranslationRunStatus.ProviderUnavailable, Reason = reason };

    public static TranslationRunResult Fail(string reason, Exception? ex = null)
        => new() { Status = TranslationRunStatus.Failed, Reason = reason, Exception = ex };
}
