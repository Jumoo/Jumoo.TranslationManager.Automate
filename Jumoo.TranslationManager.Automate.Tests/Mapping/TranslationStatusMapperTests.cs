using Jumoo.TranslationManager.Automate.Mapping;
using Jumoo.TranslationManager.Core.Models;
using NUnit.Framework;

namespace Jumoo.TranslationManager.Automate.Tests.Mapping;

public class TranslationStatusMapperTests
{
    [TestCase(JobStatus.Created, false)]
    [TestCase(JobStatus.Submitted, false)]
    [TestCase(JobStatus.Processing, false)]
    [TestCase(JobStatus.Received, true)]
    [TestCase(JobStatus.PartialApproved, true)]
    [TestCase(JobStatus.Accepted, true)]
    [TestCase(JobStatus.Closed, true)]
    [TestCase(JobStatus.Error, false)]
    public void IsComplete_MatchesExpected(JobStatus status, bool expected)
        => Assert.That(TranslationStatusMapper.IsComplete(status), Is.EqualTo(expected));

    [Test]
    public void IsError_TrueOnlyForError()
    {
        Assert.That(TranslationStatusMapper.IsError(JobStatus.Error), Is.True);
        Assert.That(TranslationStatusMapper.IsError(JobStatus.Received), Is.False);
    }

    [TestCase(JobStatus.MAX_SUBMITTED)]
    [TestCase(JobStatus.STATUSMAX)]
    [TestCase(JobStatus.ARCHIVE_MAX)]
    public void ToName_NeverEmitsSentinelValues(JobStatus sentinel)
        => Assert.That(TranslationStatusMapper.ToName(sentinel), Is.EqualTo("Unknown"));

    [Test]
    public void ToName_MapsKnownStatuses()
    {
        Assert.That(TranslationStatusMapper.ToName(JobStatus.Received), Is.EqualTo("Received"));
        Assert.That(TranslationStatusMapper.ToName(JobStatus.Error), Is.EqualTo("Error"));
    }
}
