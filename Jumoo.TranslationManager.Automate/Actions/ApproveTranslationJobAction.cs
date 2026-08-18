using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Services;
using Umbraco.Automate.Core.Actions;
using Umbraco.Cms.Core.Actions;

namespace Jumoo.TranslationManager.Automate.Actions;

/// <summary>
/// Approves a translation job, optionally publishing the translated content. Approval always
/// happens synchronously with Translation Manager's own background approval path disabled -
/// see <see cref="TranslationRunner"/> for the loop-protection rationale.
/// </summary>
[Action("translationManager.approveJob", "Approve Translation Job",
    Description = "Approves a translation job and optionally publishes the translated content.",
    Group = TranslationManagerAutomateConstants.Group,
    Icon = "icon-check",
    RequiredSections = [TranslationManagerAutomateConstants.SectionAlias],
    RequiredPermissions = [ActionUpdate.ActionLetter, ActionPublish.ActionLetter])]
public sealed class ApproveTranslationJobAction
    : ActionBase<ApproveTranslationJobSettings, Models.TranslationJobOutput>, ICmsAction
{
    private readonly TranslationRunner _runner;

    public ApproveTranslationJobAction(ActionInfrastructure infrastructure, TranslationRunner runner)
        : base(infrastructure)
    {
        _runner = runner;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ApproveTranslationJobSettings>();

        if (!Guid.TryParse(settings.JobKey, out var jobKey) || jobKey == Guid.Empty)
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing translation job key: '{settings.JobKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (context.ExecutionContext?.ServiceAccountKey is not { } serviceAccountKey)
        {
            return ActionResult.Failed(
                new InvalidOperationException(
                    "No service account identity available. Approvals must be attributed to the workspace service account."),
                StepRunErrorCategory.Authentication);
        }

        var request = new ApproveJobRequest
        {
            JobKey = jobKey,
            Check = settings.Check,
            Publish = settings.Publish,
            ApproveAllNodes = settings.ApproveAllNodes,
            NodeKeys = CsvParsing.ParseGuidsOrNull(settings.NodeKeys),
            ServiceAccountKey = serviceAccountKey,
        };

        var result = await _runner.ApproveJobAsync(request, cancellationToken);

        return result.Status switch
        {
            TranslationRunStatus.Success => Success(result.Output!),
            TranslationRunStatus.Validation => ActionResult.Failed(
                new InvalidOperationException(result.Reason), StepRunErrorCategory.Validation),
            _ => ActionResult.Failed(
                result.Exception ?? new InvalidOperationException(result.Reason ?? "Approving the translation job failed."),
                StepRunErrorCategory.Unknown),
        };
    }
}
