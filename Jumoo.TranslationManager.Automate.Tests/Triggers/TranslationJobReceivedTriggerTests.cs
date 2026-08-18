using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Services;
using Jumoo.TranslationManager.Automate.Triggers.Jobs;
using Jumoo.TranslationManager.Core;
using Jumoo.TranslationManager.Core.Models;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Services;

namespace Jumoo.TranslationManager.Automate.Tests.Triggers;

public class TranslationJobReceivedTriggerTests
{
    private static TranslationJobReceivedTrigger MakeTrigger(bool gateRunning = false)
    {
        var infrastructure = new TriggerInfrastructure(Mock.Of<IEditableModelResolver>());
        var mapper = new TranslationJobMapper(Mock.Of<IContentService>());
        var gate = Mock.Of<ITranslationOperationGate>(g => g.IsRunning == gateRunning);

        var optionsMonitor = new Mock<IOptionsMonitor<TranslationManagerAutomateOptions>>();
        optionsMonitor.SetupGet(o => o.CurrentValue).Returns(new TranslationManagerAutomateOptions());

        return new TranslationJobReceivedTrigger(infrastructure, mapper, gate, optionsMonitor.Object);
    }

    [Test]
    public void MapEvent_NullJob_YieldsNothing()
    {
        var trigger = MakeTrigger();
        var notification = new TranslationJobReceivedNotification { Job = null! };

        Assert.That(trigger.MapEvent(notification), Is.Empty);
    }

    [Test]
    public void MapEvent_YieldsOneEventWithJobScopedIdempotencyKey()
    {
        var trigger = MakeTrigger();
        var job = new TranslationJob
        {
            Key = Guid.NewGuid(),
            Received = DateTime.UtcNow,
            Status = JobStatus.Received,
            TargetCulture = new CultureInfoView { Name = "fr-FR" },
        };
        var notification = new TranslationJobReceivedNotification { Job = job, Publish = true, UserKey = Guid.NewGuid() };

        var events = trigger.MapEvent(notification).ToList();

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].TriggerAlias, Is.EqualTo("translationManager.jobReceived"));
        Assert.That(events[0].IdempotencyKey, Does.Contain(job.Key.ToString()));
        Assert.That(((TriggerEvent<TranslationJobTriggerOutput>)events[0]).Output.TargetCulture, Is.EqualTo("fr-FR"));
    }

    [Test]
    public void MapEvent_DuplicateNotification_SameReceivedStamp_ProducesSameIdempotencyKey()
    {
        var trigger = MakeTrigger();
        var received = DateTime.UtcNow;
        var jobKey = Guid.NewGuid();

        TranslationJobReceivedNotification MakeNotification() => new()
        {
            Job = new TranslationJob { Key = jobKey, Received = received, Status = JobStatus.Received },
        };

        var key1 = trigger.MapEvent(MakeNotification()).Single().IdempotencyKey;
        var key2 = trigger.MapEvent(MakeNotification()).Single().IdempotencyKey;

        Assert.That(key1, Is.EqualTo(key2));
    }

    [Test]
    public void CanHandle_GateRunningAndSuppressionOn_ReturnsFalse()
    {
        var trigger = MakeTrigger(gateRunning: true);
        var output = new TranslationJobTriggerOutput();

        Assert.That(((ITrigger)trigger).CanHandle(output, null), Is.False);
    }

    [Test]
    public void CanHandle_GateNotRunning_ReturnsTrue()
    {
        var trigger = MakeTrigger(gateRunning: false);
        var output = new TranslationJobTriggerOutput();

        Assert.That(((ITrigger)trigger).CanHandle(output, null), Is.True);
    }
}
