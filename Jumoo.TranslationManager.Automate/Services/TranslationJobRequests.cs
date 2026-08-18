namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>Input to <see cref="TranslationRunner.CheckJobAsync"/>.</summary>
public sealed class CheckJobRequest
{
    public required Guid JobKey { get; init; }
    public bool UpdateStatus { get; init; } = true;
}

/// <summary>Input to <see cref="TranslationRunner.ApproveJobAsync"/>.</summary>
public sealed class ApproveJobRequest
{
    public required Guid JobKey { get; init; }
    public bool Check { get; init; } = true;
    public bool Publish { get; init; } = true;
    public bool ApproveAllNodes { get; init; } = true;
    public Guid[]? NodeKeys { get; init; }
    public required Guid ServiceAccountKey { get; init; }
}
