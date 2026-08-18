using Jumoo.TranslationManager.Automate.Actions;
using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Testing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Jumoo.TranslationManager.Automate.Tests.Actions;

public class TranslateContentActionTests
{
    // TranslationRunner is not virtual/mockable - these tests short-circuit before it is
    // ever invoked, so a real instance over trivial mocked collaborators is enough.
    private static TranslationRunner MakeRunner()
    {
        var optionsMonitor = new Mock<IOptionsMonitor<TranslationManagerAutomateOptions>>();
        optionsMonitor.SetupGet(o => o.CurrentValue).Returns(new TranslationManagerAutomateOptions());

        return new TranslationRunner(
            Mock.Of<ITranslationManagerFacade>(),
            Mock.Of<ITranslationOperationGate>(),
            Mock.Of<ITranslationIdentityResolver>(),
            Mock.Of<IContentService>(),
            Mock.Of<IAutomationActionAuthorizer>(),
            optionsMonitor.Object,
            Mock.Of<ILogger<TranslationRunner>>());
    }

    private static AutomationExecutionContext MakeExecutionContext(Guid? serviceAccountKey = null)
        => new()
        {
            ServiceAccountKey = serviceAccountKey ?? Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            WorkspaceName = "Test Workspace",
            AutomationId = Guid.NewGuid(),
            AutomationName = "Test Automation",
            RunId = Guid.NewGuid(),
            InitiatorType = "test",
            AllowedConnections = [],
        };

    private static Mock<IAutomationActionAuthorizer> MakeAuthorizer(bool authorized = true)
    {
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer.Setup(a => a.AuthorizeContentAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorized
                ? new AutomationAuthorizationResult(true, null)
                : new AutomationAuthorizationResult(false, "denied"));
        return authorizer;
    }

    private static Mock<IUmbracoContextFactory> MakeContextFactory()
    {
        var factory = new Mock<IUmbracoContextFactory>();
        var reference = new UmbracoContextReference(Mock.Of<IUmbracoContext>(), true, Mock.Of<IUmbracoContextAccessor>());
        factory.Setup(f => f.EnsureUmbracoContext()).Returns(reference);
        return factory;
    }

    [Test]
    public async Task ExecuteAsync_MissingContentKey_ReturnsValidationFailure()
    {
        var result = await ActionTestHarness.For<TranslateContentAction>()
            .WithService(MakeRunner())
            .WithService(MakeAuthorizer().Object)
            .WithService(MakeContextFactory().Object)
            .WithSettings(new TranslateContentSettings { ContentKey = null })
            .WithExecutionContext(MakeExecutionContext())
            .ExecuteAsync();

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(result.ErrorCategory, Is.EqualTo(StepRunErrorCategory.Validation));
    }

    [Test]
    public async Task ExecuteAsync_NonGuidContentKey_ReturnsValidationFailure()
    {
        var result = await ActionTestHarness.For<TranslateContentAction>()
            .WithService(MakeRunner())
            .WithService(MakeAuthorizer().Object)
            .WithService(MakeContextFactory().Object)
            .WithSettings(new TranslateContentSettings { ContentKey = "not-a-guid" })
            .WithExecutionContext(MakeExecutionContext())
            .ExecuteAsync();

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(result.ErrorCategory, Is.EqualTo(StepRunErrorCategory.Validation));
    }

    [Test]
    public async Task ExecuteAsync_AuthorizerDenies_ReturnsAuthenticationFailure()
    {
        var result = await ActionTestHarness.For<TranslateContentAction>()
            .WithService(MakeRunner())
            .WithService(MakeAuthorizer(authorized: false).Object)
            .WithService(MakeContextFactory().Object)
            .WithSettings(new TranslateContentSettings { ContentKey = Guid.NewGuid().ToString() })
            .WithExecutionContext(MakeExecutionContext())
            .ExecuteAsync();

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(result.ErrorCategory, Is.EqualTo(StepRunErrorCategory.Authentication));
    }
}
