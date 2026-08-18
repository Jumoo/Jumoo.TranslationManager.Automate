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

public class TranslationRunnerTests
{
    private Mock<ITranslationManagerFacade> _facade = null!;
    private Mock<ITranslationOperationGate> _gate = null!;
    private Mock<ITranslationIdentityResolver> _identity = null!;
    private Mock<IContentService> _contentService = null!;
    private Mock<IAutomationActionAuthorizer> _authorizer = null!;
    private TranslationManagerAutomateOptions _options = null!;
    private TranslationRunner _runner = null!;

    private static readonly Guid ServiceAccountKey = Guid.NewGuid();
    private static readonly Guid ContentKey = Guid.NewGuid();
    private static readonly Guid ProviderKey = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _facade = new Mock<ITranslationManagerFacade>();
        _gate = new Mock<ITranslationOperationGate>();
        _identity = new Mock<ITranslationIdentityResolver>();
        _contentService = new Mock<IContentService>();
        _authorizer = new Mock<IAutomationActionAuthorizer>();
        _options = new TranslationManagerAutomateOptions();

        _gate.Setup(g => g.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDisposable>());

        var optionsMonitor = new Mock<IOptionsMonitor<TranslationManagerAutomateOptions>>();
        optionsMonitor.SetupGet(o => o.CurrentValue).Returns(() => _options);

        _runner = new TranslationRunner(
            _facade.Object,
            _gate.Object,
            _identity.Object,
            _contentService.Object,
            _authorizer.Object,
            optionsMonitor.Object,
            Mock.Of<ILogger<TranslationRunner>>());
    }

    private static IContent MakeContent(Guid key, string path = "-1,1234")
    {
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.Id).Returns(1234);
        content.SetupGet(c => c.Path).Returns(path);
        content.SetupGet(c => c.Name).Returns("Test Content");
        return content.Object;
    }

    private static TranslationRunRequest MakeRequest(
        string[]? cultures = null, Guid? providerKey = null, bool force = false,
        bool approve = false, bool publish = false, int maxJobDetails = 20)
        => new()
        {
            ContentKey = ContentKey,
            Cultures = cultures,
            ProviderKey = providerKey,
            Force = force,
            Approve = approve,
            Publish = publish,
            MaxJobDetails = maxJobDetails,
            ServiceAccountKey = ServiceAccountKey,
            GroupId = "run-1",
        };

    private static TranslationSet MakeSet(Guid? providerKey = null, params (string culture, string displayName)[] sites)
    {
        var set = new TranslationSet
        {
            Id = 1,
            Key = Guid.NewGuid(),
            Name = "Test Set",
            ProviderKey = providerKey,
            Sites = sites.Select(s => new TranslationSetSite
            {
                CultureName = s.culture,
                Culture = new CultureInfoView { Name = s.culture, DisplayName = s.displayName },
            }).ToList(),
        };
        return set;
    }

    private static TranslationNode MakeNode(string culture)
        => new() { Culture = new CultureInfoView { Name = culture, DisplayName = culture } };

    private void SetUpIdentity(IUser? user)
        => _identity.Setup(i => i.ResolveAsync(ServiceAccountKey, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private static IUser MakeUser() => Mock.Of<IUser>(u => u.Key == Guid.NewGuid());

    [Test]
    public async Task RunAsync_ContentNotFound_ReturnsValidation()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns((IContent?)null);

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Validation));
    }

    [Test]
    public async Task RunAsync_GateBusy_ReturnsBusy()
    {
        _gate.Setup(g => g.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDisposable?)null);
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Busy));
    }

    [Test]
    public async Task RunAsync_ContentInNoSet_ReturnsNothingToDo()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<TranslationSet>());

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.NothingToDo));
    }

    [Test]
    public async Task RunAsync_NoIdentity_ReturnsValidation()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French (France)"))]);
        SetUpIdentity(null);

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Validation));
    }

    [Test]
    public async Task RunAsync_ProviderInactive_ReturnsProviderUnavailable()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French (France)"))]);
        SetUpIdentity(MakeUser());

        var provider = new Mock<ITranslationProvider>();
        provider.Setup(p => p.Active()).Returns(false);
        _facade.Setup(f => f.GetProvider(ProviderKey)).Returns(provider.Object);

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.ProviderUnavailable));
    }

    [Test]
    public async Task RunAsync_NoProviderResolved_ReturnsValidation()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(providerKey: null, ("fr-FR", "French (France)"))]);
        SetUpIdentity(MakeUser());

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Validation));
    }

    private void SetUpHappyPathProvider(Mock<ITranslationProvider>? provider = null)
    {
        provider ??= new Mock<ITranslationProvider>();
        provider.Setup(p => p.Active()).Returns(true);
        provider.SetupGet(p => p.Key).Returns(ProviderKey);
        provider.SetupGet(p => p.Name).Returns("Test Provider");
        _facade.Setup(f => f.GetProvider(ProviderKey)).Returns(provider.Object);
    }

    private void SetUpCreateAndSubmit(Func<TranslationJob, TranslationJob>? mutate = null)
    {
        var nextId = 1;
        _facade.Setup(f => f.CreateJobAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<TranslationNode>>(), It.IsAny<ITranslationProvider>(),
                It.IsAny<object>(), It.IsAny<IUser>(), It.IsAny<JobOptions>(), It.IsAny<string>()))
            .ReturnsAsync((string name, IEnumerable<TranslationNode> nodes, ITranslationProvider _, object _, IUser _, JobOptions _, string groupId) =>
            {
                var job = new TranslationJob
                {
                    Id = nextId++,
                    Key = Guid.NewGuid(),
                    Name = name,
                    GroupId = groupId,
                    NodeCount = nodes.Count(),
                    TargetCulture = nodes.First().Culture,
                    Status = JobStatus.Submitted,
                };
                return mutate is null ? job : mutate(job);
            });

        _facade.Setup(f => f.LoadJobNodesAsync(It.IsAny<TranslationJob>()))
            .ReturnsAsync((TranslationJob job) => job);

        _facade.Setup(f => f.SubmitJobAsync(It.IsAny<TranslationJob>()))
            .ReturnsAsync((TranslationJob job) => Attempt<TranslationJob?>.Succeed(job));
    }

    [Test]
    public async Task RunAsync_MultipleCultures_CreatesOneJobPerCultureSharingGroupId()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"), ("de-DE", "German"), ("es-ES", "Spanish"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();

        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync((TranslationSet _, IContent _, NodeCreationOptions _, IEnumerable<TranslationSetSite> sites) =>
                sites.Select(s => MakeNode(s.CultureName)));

        SetUpCreateAndSubmit();

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Success));
        Assert.That(result.Output!.JobCount, Is.EqualTo(3));
        _facade.Verify(f => f.CreateJobAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<TranslationNode>>(), It.IsAny<ITranslationProvider>(),
            It.IsAny<object>(), It.IsAny<IUser>(), It.IsAny<JobOptions>(), "run-1"), Times.Exactly(3));
    }

    [Test]
    public async Task RunAsync_AlwaysCallsLoadJobNodesBeforeSubmit()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([MakeNode("fr-FR")]);
        SetUpCreateAndSubmit();

        var sequence = new List<string>();
        _facade.Setup(f => f.LoadJobNodesAsync(It.IsAny<TranslationJob>()))
            .ReturnsAsync((TranslationJob job) => { sequence.Add("load"); return job; });
        _facade.Setup(f => f.SubmitJobAsync(It.IsAny<TranslationJob>()))
            .ReturnsAsync((TranslationJob job) => { sequence.Add("submit"); return Attempt<TranslationJob?>.Succeed(job); });

        await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(sequence, Is.EqualTo(new[] { "load", "submit" }));
    }

    [Test]
    public async Task RunAsync_NeverSetsAutoApproveOnJobOptions()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([MakeNode("fr-FR")]);
        SetUpCreateAndSubmit();

        // Request itself carries Approve=true; JobOptions.AutoApprove must still be false -
        // approval, when requested, happens via the explicit synchronous path, never via TM's
        // own JobOptions flag (which double-approves and defeats loop protection).
        await _runner.RunAsync(MakeRequest(approve: true), CancellationToken.None);

        _facade.Verify(f => f.CreateJobAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<TranslationNode>>(), It.IsAny<ITranslationProvider>(),
            It.IsAny<object>(), It.IsAny<IUser>(),
            It.Is<JobOptions>(o => o.AutoApprove == false),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task RunAsync_Approve_NeverSetsApproveInBackground()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([MakeNode("fr-FR")]);
        SetUpCreateAndSubmit();
        _facade.Setup(f => f.ApproveJobAsync(It.IsAny<int>(), It.IsAny<TranslationJobApprovalOptions>())).ReturnsAsync(true);

        await _runner.RunAsync(MakeRequest(approve: true), CancellationToken.None);

        _facade.Verify(f => f.ApproveJobAsync(
            It.IsAny<int>(),
            It.Is<TranslationJobApprovalOptions>(o => o.ApproveInBackground == false)), Times.Once);
    }

    [Test]
    public async Task RunAsync_ForceFalse_UsesCreateChangeType()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([MakeNode("fr-FR")]);
        SetUpCreateAndSubmit();

        await _runner.RunAsync(MakeRequest(force: false), CancellationToken.None);

        _facade.Verify(f => f.CreateNodesAsync(
            It.IsAny<TranslationSet>(), It.IsAny<IContent>(),
            It.Is<NodeCreationOptions>(o => o.ChangeType == TranslationChangeType.Create),
            It.IsAny<IEnumerable<TranslationSetSite>>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_ForceTrue_UsesForceChangeType()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([MakeNode("fr-FR")]);
        SetUpCreateAndSubmit();

        await _runner.RunAsync(MakeRequest(force: true), CancellationToken.None);

        _facade.Verify(f => f.CreateNodesAsync(
            It.IsAny<TranslationSet>(), It.IsAny<IContent>(),
            It.Is<NodeCreationOptions>(o => o.ChangeType == TranslationChangeType.Force),
            It.IsAny<IEnumerable<TranslationSetSite>>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_CultureFilter_MatchesCultureNameNotDisplayName()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        // A set with one site whose culture code is fr-FR and display name "French (France)".
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French (France)"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();

        IEnumerable<TranslationSetSite>? capturedSites = null;
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .Callback((TranslationSet _, IContent _, NodeCreationOptions _, IEnumerable<TranslationSetSite> sites) => capturedSites = sites.ToList())
            .ReturnsAsync([]);

        // Filtering by the display name value must produce zero matching sites.
        await _runner.RunAsync(MakeRequest(cultures: ["French (France)"]), CancellationToken.None);

        Assert.That(capturedSites, Is.Not.Null);
        Assert.That(capturedSites!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task RunAsync_CultureFilter_MatchesCultureNameCode()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French (France)"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();

        IEnumerable<TranslationSetSite>? capturedSites = null;
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .Callback((TranslationSet _, IContent _, NodeCreationOptions _, IEnumerable<TranslationSetSite> sites) => capturedSites = sites.ToList())
            .ReturnsAsync([]);

        await _runner.RunAsync(MakeRequest(cultures: ["fr-FR"]), CancellationToken.None);

        Assert.That(capturedSites, Is.Not.Null);
        Assert.That(capturedSites!.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_SubmitFails_ReturnsFailedWithException()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([MakeNode("fr-FR")]);

        var boom = new InvalidOperationException("connector rejected job");
        _facade.Setup(f => f.CreateJobAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<TranslationNode>>(), It.IsAny<ITranslationProvider>(),
                It.IsAny<object>(), It.IsAny<IUser>(), It.IsAny<JobOptions>(), It.IsAny<string>()))
            .ReturnsAsync(new TranslationJob { Id = 1, Key = Guid.NewGuid(), TargetCulture = new CultureInfoView { Name = "fr-FR" } });
        _facade.Setup(f => f.LoadJobNodesAsync(It.IsAny<TranslationJob>())).ReturnsAsync((TranslationJob job) => job);
        _facade.Setup(f => f.SubmitJobAsync(It.IsAny<TranslationJob>()))
            .ReturnsAsync((TranslationJob job) =>
            {
                Attempt<TranslationJob?> failed = Attempt<TranslationJob?>.Fail(job, boom);
                return failed;
            });

        var result = await _runner.RunAsync(MakeRequest(), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Failed));
        Assert.That(result.Exception, Is.SameAs(boom));
    }

    [Test]
    public async Task RunAsync_MaxJobDetails_CapsJobsAndSetsTruncated()
    {
        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(ProviderKey, ("fr-FR", "French"), ("de-DE", "German"), ("es-ES", "Spanish"))]);
        SetUpIdentity(MakeUser());
        SetUpHappyPathProvider();
        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync((TranslationSet _, IContent _, NodeCreationOptions _, IEnumerable<TranslationSetSite> sites) =>
                sites.Select(s => MakeNode(s.CultureName)));
        SetUpCreateAndSubmit();

        var result = await _runner.RunAsync(MakeRequest(maxJobDetails: 2), CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(TranslationRunStatus.Success));
        Assert.That(result.Output!.JobCount, Is.EqualTo(3));
        Assert.That(result.Output!.Jobs, Has.Length.EqualTo(2));
        Assert.That(result.Output!.JobsTruncated, Is.True);
    }

    [Test]
    public async Task RunAsync_ProviderPrecedence_ExplicitBeatsSetBeatsDefault()
    {
        var explicitProvider = Guid.NewGuid();
        var setProvider = Guid.NewGuid();

        _contentService.Setup(c => c.GetById(ContentKey)).Returns(MakeContent(ContentKey));
        _facade.Setup(f => f.GetSetsByPathAsync(It.IsAny<string>()))
            .ReturnsAsync([MakeSet(setProvider, ("fr-FR", "French"))]);
        SetUpIdentity(MakeUser());
        _options.DefaultProviderKey = Guid.NewGuid();

        var provider = new Mock<ITranslationProvider>();
        provider.Setup(p => p.Active()).Returns(true);
        provider.SetupGet(p => p.Key).Returns(explicitProvider);
        _facade.Setup(f => f.GetProvider(explicitProvider)).Returns(provider.Object);

        _facade.Setup(f => f.CreateNodesAsync(
                It.IsAny<TranslationSet>(), It.IsAny<IContent>(), It.IsAny<NodeCreationOptions>(), It.IsAny<IEnumerable<TranslationSetSite>>()))
            .ReturnsAsync([]);

        await _runner.RunAsync(MakeRequest(providerKey: explicitProvider), CancellationToken.None);

        _facade.Verify(f => f.GetProvider(explicitProvider), Times.Once);
        _facade.Verify(f => f.GetProvider(setProvider), Times.Never);
    }
}
