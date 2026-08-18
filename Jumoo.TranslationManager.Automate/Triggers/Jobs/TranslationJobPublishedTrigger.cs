using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Mapping;
using Jumoo.TranslationManager.Automate.Services;
using Jumoo.TranslationManager.Core;
using Jumoo.TranslationManager.Core.Models;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;

namespace Jumoo.TranslationManager.Automate.Triggers.Jobs;

/// <summary>Fires when a translation job's content has been published.</summary>
[Trigger("translationManager.jobPublished", "Translation Published",
    Description = "Fires when a translation job's content has been published.",
    Group = TranslationManagerAutomateConstants.Group,
    Icon = "icon-globe",
    RequiredSections = [TranslationManagerAutomateConstants.SectionAlias])]
public sealed class TranslationJobPublishedTrigger
    : NotificationTriggerBase<TranslationJobTriggerSettings, TranslationJobTriggerOutput, TranslationJobPublishedNotification>
{
    private readonly TranslationJobMapper _mapper;
    private readonly ITranslationOperationGate _gate;
    private readonly IOptionsMonitor<TranslationManagerAutomateOptions> _options;

    public TranslationJobPublishedTrigger(
        TriggerInfrastructure infrastructure,
        TranslationJobMapper mapper,
        ITranslationOperationGate gate,
        IOptionsMonitor<TranslationManagerAutomateOptions> options)
        : base(infrastructure)
    {
        _mapper = mapper;
        _gate = gate;
        _options = options;
    }

    public override IEnumerable<TriggerEvent> MapEvent(TranslationJobPublishedNotification notification)
    {
        if (notification.Job is not { } job) yield break;

        var approvedNodeCount = job.Nodes?.Count(n => n.Status == NodeStatus.Approved) ?? 0;

        yield return new TriggerEvent<TranslationJobTriggerOutput>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = TranslationIdempotency.ForJobProgress(Alias, job, approvedNodeCount),
            Output = _mapper.Map(job, notification.Publish, notification.UserKey, _options.CurrentValue.MaxContentKeysPerEvent),
        };
    }

    protected override bool CanHandle(TranslationJobTriggerOutput output, TranslationJobTriggerSettings? settings)
    {
        // Suppress our own writes from re-entering as trigger input while this package's
        // runner is actively performing a translation write - see TranslationRunner's
        // remarks for the full loop-protection rationale.
        if (_options.CurrentValue.SuppressSelfOriginatedEvents && _gate.IsRunning)
            return false;

        return TranslationJobTriggerFilter.Matches(output, settings);
    }
}
