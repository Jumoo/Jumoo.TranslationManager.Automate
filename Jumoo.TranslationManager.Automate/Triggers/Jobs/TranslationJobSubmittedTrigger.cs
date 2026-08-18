using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Mapping;
using Jumoo.TranslationManager.Automate.Services;
using Jumoo.TranslationManager.Core;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Triggers;

namespace Jumoo.TranslationManager.Automate.Triggers.Jobs;

/// <summary>Fires when a translation job is submitted to a connector.</summary>
[Trigger("translationManager.jobSubmitted", "Translation Job Submitted",
    Description = "Fires when a translation job is submitted to a translation connector.",
    Group = TranslationManagerAutomateConstants.Group,
    Icon = "icon-out",
    RequiredSections = [TranslationManagerAutomateConstants.SectionAlias])]
public sealed class TranslationJobSubmittedTrigger
    : NotificationTriggerBase<TranslationJobTriggerSettings, TranslationJobTriggerOutput, TranslationJobSubmittedNotification>
{
    private readonly TranslationJobMapper _mapper;
    private readonly ITranslationOperationGate _gate;
    private readonly IOptionsMonitor<TranslationManagerAutomateOptions> _options;

    public TranslationJobSubmittedTrigger(
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

    public override IEnumerable<TriggerEvent> MapEvent(TranslationJobSubmittedNotification notification)
    {
        if (notification.Job is not { } job) yield break;

        yield return new TriggerEvent<TranslationJobTriggerOutput>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = TranslationIdempotency.ForJobEvent(Alias, job, job.Submitted),
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
