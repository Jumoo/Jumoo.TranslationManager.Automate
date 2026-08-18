namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>Input to <see cref="TranslationRunner.RunAsync"/>.</summary>
public sealed class TranslationRunRequest
{
    public required Guid ContentKey { get; init; }
    public bool IncludeDescendants { get; init; }
    public string[]? Cultures { get; init; }
    public Guid[]? SetKeys { get; init; }
    public Guid? ProviderKey { get; init; }
    public string? JobNameTemplate { get; init; }

    /// <summary>TranslationChangeType.Force vs .Create - see the loop-protection notes on
    /// <see cref="TranslationRunner"/> for why <c>false</c> (Create) is the safer default.</summary>
    public bool Force { get; init; }

    /// <summary>Approve each job synchronously once submitted. Only sensible with a connector
    /// that returns instantly (e.g. passthrough/machine translation).</summary>
    public bool Approve { get; init; }

    /// <summary>Publish the translated content after approving. Only applies with <see cref="Approve"/>.</summary>
    public bool Publish { get; init; }

    public int MaxJobDetails { get; init; } = 20;

    public required Guid ServiceAccountKey { get; init; }
    public required string GroupId { get; init; }
    public string? PerformingDetails { get; init; }
}
