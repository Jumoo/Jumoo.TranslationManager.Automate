namespace Jumoo.TranslationManager.Automate.Actions.Models;

/// <summary>Output of the <c>translationManager.translateContent</c> action.</summary>
public sealed class TranslateContentOutput
{
    public bool Submitted { get; init; }
    public bool Approved { get; init; }
    public int JobCount { get; init; }
    public int NodeCount { get; init; }

    /// <summary>Flat arrays so <c>${translate.jobKeys}</c> feeds straight into <c>umbracoAutomate.forEach</c>.</summary>
    public int[] JobIds { get; init; } = [];
    public Guid[] JobKeys { get; init; } = [];
    public string[] Cultures { get; init; } = [];
    public string[] SetNames { get; init; } = [];

    public Guid ProviderKey { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;

    /// <summary>
    /// Capped at the run's MaxJobDetails - the output is persisted in the WorkflowCore run
    /// row and re-serialised on every binding evaluation, so this must stay small.
    /// </summary>
    public TranslationJobSummary[] Jobs { get; init; } = [];
    public bool JobsTruncated { get; init; }

    public DateTime CompletedUtc { get; init; }
}
