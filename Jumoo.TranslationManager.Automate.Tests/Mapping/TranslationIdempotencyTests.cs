using Jumoo.TranslationManager.Automate.Mapping;
using Jumoo.TranslationManager.Core.Models;
using NUnit.Framework;

namespace Jumoo.TranslationManager.Automate.Tests.Mapping;

public class TranslationIdempotencyTests
{
    private static TranslationJob MakeJob(Guid key, JobStatus status, DateTime? submitted = null, DateTime? received = null)
        => new() { Key = key, Status = status, Submitted = submitted, Received = received };

    [Test]
    public void ForJobEvent_SameJobSameStamp_ProducesSameKey()
    {
        var key = Guid.NewGuid();
        var stamp = DateTime.UtcNow;
        var job1 = MakeJob(key, JobStatus.Submitted, submitted: stamp);
        var job2 = MakeJob(key, JobStatus.Submitted, submitted: stamp);

        Assert.That(
            TranslationIdempotency.ForJobEvent("alias", job1, job1.Submitted),
            Is.EqualTo(TranslationIdempotency.ForJobEvent("alias", job2, job2.Submitted)));
    }

    [Test]
    public void ForJobEvent_DifferentStamp_ProducesDifferentKey()
    {
        var key = Guid.NewGuid();
        var job1 = MakeJob(key, JobStatus.Submitted, submitted: DateTime.UtcNow);
        var job2 = MakeJob(key, JobStatus.Submitted, submitted: DateTime.UtcNow.AddMinutes(5));

        Assert.That(
            TranslationIdempotency.ForJobEvent("alias", job1, job1.Submitted),
            Is.Not.EqualTo(TranslationIdempotency.ForJobEvent("alias", job2, job2.Submitted)));
    }

    [Test]
    public void ForJobProgress_DifferentApprovedCount_ProducesDifferentKey()
    {
        var key = Guid.NewGuid();
        var job = MakeJob(key, JobStatus.PartialApproved);

        Assert.That(
            TranslationIdempotency.ForJobProgress("alias", job, 2),
            Is.Not.EqualTo(TranslationIdempotency.ForJobProgress("alias", job, 4)));
    }

    [Test]
    public void ForJobProgress_SameApprovedCount_ProducesSameKey_DedupesRepeatApproval()
    {
        var key = Guid.NewGuid();
        var job = MakeJob(key, JobStatus.Accepted);

        Assert.That(
            TranslationIdempotency.ForJobProgress("alias", job, 3),
            Is.EqualTo(TranslationIdempotency.ForJobProgress("alias", job, 3)));
    }
}
