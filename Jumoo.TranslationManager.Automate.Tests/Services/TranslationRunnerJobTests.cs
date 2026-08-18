using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Services;
using Jumoo.TranslationManager.Core.Models;
using Jumoo.TranslationManager.Core.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Jumoo.TranslationManager.Automate.Tests.Services;

public class TranslationRunnerJobTests
{
    private Mock<ITranslationManagerFacade> _facade = null!;
    private Mock<ITranslationOperationGate> _gate = null!;
    private Mock<ITranslationIdentityResolver> _identity = null!;
    private Mock<IContentService> _contentService = null!;
    private Mock<IAutomationActionAuthorizer> _authorizer = null!;
    private TranslationRunner _runner = null!;

    private static readonly Guid ServiceAccountKey = Guid.NewGuid();
    private static readonly Guid JobKey = Guid.NewGuid();
    private static readonly Guid ProviderKey = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _facade = new Mock<ITranslationManagerFacade>();
        _gate = new Mock<ITranslationOperationGate>();
        _identity = new Mock<ITranslationIdentityResolver>();
        _contentService = new Mock<IContentService>();
        _authorizer = new Mock<IAutomationActionAuthorizer>();

        var options = new TranslationManagerAutomateOptions();
        var optionsMonitor = new Mock<IOptionsMonitor<TranslationManagerAutomateOptions>>();
        optionsMonitor.SetupGet(o => o.CurrentValue).Returns(options);

        _runner = new TranslationRunner(
            _facade.Object,
            _gate.Object,
            _identity.Object,
            _contentService.Object,
            _authorizer.Object,
            optionsMonitor.Object,
            Mock.Of<ILogger<TranslationRunner>>());
    }

    private static TranslationJob MakeJob(JobStatus status = JobStatus.Submitted, List<TranslationNode>? nodes = null)
        => new()
        {
            Id = 1,
            Key = JobKey,
            Name = "Test Job",
            ProviderKey = ProviderKey,
            ProviderName = "Test Provider",
            Status = status,
            TargetCulture = new CultureInfoView { Name = "fr-FR" },
            Nodes = nodes ?? [],
        };

    private static IUser MakeUser() => Mock.Of<IUser>(u => u.Key == Guid.NewGuid());

    [Test]
    public async Task CheckJobAsync_JobNotFound_ReturnsValidation()
    {
        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync((TranslationJob?)null);

        var result = await _runner.CheckJobAsync(new CheckJobRequest { JobKey = JobKey }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Validation));
    }

    [Test]
    public async Task CheckJobAsync_UpdateStatusTrue_CallsProviderCheckAndSavesOnChange()
    {
        var job = MakeJob(JobStatus.Submitted);
        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync(job);

        var provider = new Mock<ITranslationProvider>();
        var returnedJob = MakeJob(JobStatus.Received);
        provider.Setup(p => p.Check(job)).ReturnsAsync(Attempt<TranslationJob>.Succeed(returnedJob));
        _facade.Setup(f => f.GetProvider(ProviderKey)).Returns(provider.Object);
        _facade.Setup(f => f.SaveJobAsync(returnedJob)).ReturnsAsync(returnedJob);

        var result = await _runner.CheckJobAsync(new CheckJobRequest { JobKey = JobKey, UpdateStatus = true }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Success));
        Assert.That(result.Output!.StatusChanged, Is.True);
        Assert.That(result.Output!.Status, Is.EqualTo("Received"));
        _facade.Verify(f => f.SaveJobAsync(returnedJob), Times.Once);
    }

    [Test]
    public async Task CheckJobAsync_UpdateStatusFalse_DoesNotCallProvider()
    {
        var job = MakeJob(JobStatus.Submitted);
        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync(job);

        var result = await _runner.CheckJobAsync(new CheckJobRequest { JobKey = JobKey, UpdateStatus = false }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Success));
        Assert.That(result.Output!.StatusChanged, Is.False);
        _facade.Verify(f => f.GetProvider(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task ApproveJobAsync_JobNotFound_ReturnsValidation()
    {
        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync((TranslationJob?)null);

        var result = await _runner.ApproveJobAsync(
            new ApproveJobRequest { JobKey = JobKey, ServiceAccountKey = ServiceAccountKey }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Validation));
    }

    [Test]
    public async Task ApproveJobAsync_NoIdentity_ReturnsValidation()
    {
        var job = MakeJob();
        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync(job);
        _identity.Setup(i => i.ResolveAsync(ServiceAccountKey, It.IsAny<CancellationToken>())).ReturnsAsync((IUser?)null);

        var result = await _runner.ApproveJobAsync(
            new ApproveJobRequest { JobKey = JobKey, ServiceAccountKey = ServiceAccountKey }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Validation));
    }

    [Test]
    public async Task ApproveJobAsync_UnauthorizedContent_FailsClosedWithoutApproving()
    {
        var content = Mock.Of<IContent>(c => c.Key == Guid.NewGuid());
        var node = new TranslationNode { MasterNodeId = 42 };
        var job = MakeJob(nodes: [node]);

        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync(job);
        _identity.Setup(i => i.ResolveAsync(ServiceAccountKey, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser());
        _facade.Setup(f => f.LoadJobNodesAsync(job)).ReturnsAsync(job);
        _contentService.Setup(c => c.GetById(42)).Returns(content);

        // Authorizer denies the content - fewer authorized keys than requested.
        _authorizer.Setup(a => a.FilterAuthorizedContentAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        var result = await _runner.ApproveJobAsync(
            new ApproveJobRequest { JobKey = JobKey, ServiceAccountKey = ServiceAccountKey }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Failed));
        _facade.Verify(f => f.ApproveJobAsync(It.IsAny<int>(), It.IsAny<TranslationJobApprovalOptions>()), Times.Never);
    }

    [Test]
    public async Task ApproveJobAsync_Authorized_ApprovesWithApproveInBackgroundFalse()
    {
        var content = Mock.Of<IContent>(c => c.Key == Guid.NewGuid());
        var node = new TranslationNode { MasterNodeId = 42 };
        var job = MakeJob(nodes: [node]);

        _facade.Setup(f => f.GetJobByKeyAsync(JobKey)).ReturnsAsync(job);
        _identity.Setup(i => i.ResolveAsync(ServiceAccountKey, It.IsAny<CancellationToken>())).ReturnsAsync(MakeUser());
        _facade.Setup(f => f.LoadJobNodesAsync(job)).ReturnsAsync(job);
        _contentService.Setup(c => c.GetById(42)).Returns(content);

        _authorizer.Setup(a => a.FilterAuthorizedContentAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { content.Key });

        _facade.Setup(f => f.ApproveJobAsync(job.Id, It.IsAny<TranslationJobApprovalOptions>())).ReturnsAsync(true);
        _facade.Setup(f => f.GetJobByKeyAsync(job.Key)).ReturnsAsync(job);

        var result = await _runner.ApproveJobAsync(
            new ApproveJobRequest { JobKey = JobKey, ServiceAccountKey = ServiceAccountKey, Publish = true }, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Success));
        Assert.That(result.Output!.Approved, Is.True);
        Assert.That(result.Output!.Published, Is.True);
        _facade.Verify(f => f.ApproveJobAsync(
            job.Id,
            It.Is<TranslationJobApprovalOptions>(o => o.ApproveInBackground == false && o.Publish == true)), Times.Once);
    }
}
