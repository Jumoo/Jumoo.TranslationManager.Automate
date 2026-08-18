using Jumoo.TranslationManager.Automate.Actions.Models;
using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Services;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Web;

namespace Jumoo.TranslationManager.Automate.Actions;

/// <summary>
/// Creates translation nodes for a content item, groups them into one job per target culture,
/// and submits those jobs to a translation connector. <see cref="ICmsAction"/> so the work is
/// audited against the workspace service account.
/// </summary>
[Action("translationManager.translateContent", "Translate Content",
    Description = "Creates and submits Translation Manager jobs for a content item.",
    Group = TranslationManagerAutomateConstants.Group,
    Icon = "icon-globe",
    RequiredSections = [TranslationManagerAutomateConstants.SectionAlias],
    RequiredPermissions = [ActionUpdate.ActionLetter])]
public sealed class TranslateContentAction
    : ActionBase<TranslateContentSettings, TranslateContentOutput>, ICmsAction
{
    private readonly TranslationRunner _runner;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IUmbracoContextFactory _umbracoContextFactory;

    public TranslateContentAction(
        ActionInfrastructure infrastructure,
        TranslationRunner runner,
        IAutomationActionAuthorizer authorizer,
        IUmbracoContextFactory umbracoContextFactory)
        : base(infrastructure)
    {
        _runner = runner;
        _authorizer = authorizer;
        _umbracoContextFactory = umbracoContextFactory;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<TranslateContentSettings>();

        if (!Guid.TryParse(settings.ContentKey, out var contentKey) || contentKey == Guid.Empty)
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeContentOrFailAsync(contentKey, RequiredPermissions, cancellationToken) is { } denied)
            return denied;

        if (context.ExecutionContext?.ServiceAccountKey is not { } serviceAccountKey)
        {
            return ActionResult.Failed(
                new InvalidOperationException(
                    "No service account identity available. Translation jobs must be attributed to the workspace service account."),
                StepRunErrorCategory.Authentication);
        }

        var request = new TranslationRunRequest
        {
            ContentKey = contentKey,
            IncludeDescendants = settings.IncludeDescendants,
            Cultures = CsvParsing.ParseOrNull(settings.Cultures),
            SetKeys = CsvParsing.ParseGuidsOrNull(settings.SetKeys),
            ProviderKey = Guid.TryParse(settings.ProviderKey, out var providerKey) ? providerKey : null,
            JobNameTemplate = settings.JobNameTemplate,
            Force = settings.Force,
            Approve = settings.Approve,
            Publish = settings.Publish,
            MaxJobDetails = settings.MaxJobDetails,
            ServiceAccountKey = serviceAccountKey,
            GroupId = context.RunId.ToString(),
            PerformingDetails = context.ExecutionContext.FormatPerformingDetails(),
        };

        // The approve path writes and publishes content, raising notifications that resolve
        // URLs via UrlProvider. The outbox dispatcher has no HTTP scope, so there is no
        // ambient UmbracoContext without this.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _runner.RunAsync(request, cancellationToken);

        return result.Status switch
        {
            TranslationRunStatus.Success => Success(result.Output!),
            TranslationRunStatus.NothingToDo => ActionResult.Skipped(result.Reason),
            TranslationRunStatus.Busy => ActionResult.Skipped(result.Reason),
            TranslationRunStatus.Validation => ActionResult.Failed(
                new InvalidOperationException(result.Reason), StepRunErrorCategory.Validation),
            TranslationRunStatus.ProviderUnavailable => ActionResult.Failed(
                result.Exception ?? new InvalidOperationException(result.Reason),
                StepRunErrorCategory.ServiceUnavailable),
            _ => ActionResult.Failed(
                result.Exception ?? new InvalidOperationException(result.Reason ?? "Translation failed."),
                StepRunErrorCategory.Unknown),
        };
    }
}
