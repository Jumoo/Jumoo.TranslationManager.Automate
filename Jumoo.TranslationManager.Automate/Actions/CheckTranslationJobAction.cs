using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Services;
using Umbraco.Automate.Core.Actions;
using Umbraco.Cms.Core.Actions;

namespace Jumoo.TranslationManager.Automate.Actions;

/// <summary>
/// Polls a translation connector for the latest status of a job. Returns its outcome as the
/// mapped job status, so a workflow can branch directly on it without a separate If step.
/// </summary>
[Action("translationManager.checkJob", "Check Translation Job",
    Description = "Checks a translation connector for the latest status of a translation job.",
    Group = TranslationManagerAutomateConstants.Group,
    Icon = "icon-time",
    RequiredSections = [TranslationManagerAutomateConstants.SectionAlias],
    RequiredPermissions = [ActionBrowse.ActionLetter])]
public sealed class CheckTranslationJobAction
    : ActionBase<CheckTranslationJobSettings, Models.TranslationJobOutput>
{
    private readonly TranslationRunner _runner;

    public CheckTranslationJobAction(ActionInfrastructure infrastructure, TranslationRunner runner)
        : base(infrastructure)
    {
        _runner = runner;
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<CheckTranslationJobSettings>();

        if (!Guid.TryParse(settings.JobKey, out var jobKey) || jobKey == Guid.Empty)
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing translation job key: '{settings.JobKey}'."),
                StepRunErrorCategory.Validation);
        }

        var result = await _runner.CheckJobAsync(
            new CheckJobRequest { JobKey = jobKey, UpdateStatus = settings.UpdateStatus },
            cancellationToken);

        return result.Status switch
        {
            TranslationRunStatus.Success => SuccessWithOutcome(result.Output!.Status, result.Output),
            TranslationRunStatus.Validation => ActionResult.Failed(
                new InvalidOperationException(result.Reason), StepRunErrorCategory.Validation),
            _ => ActionResult.Failed(
                result.Exception ?? new InvalidOperationException(result.Reason ?? "Checking the translation job failed."),
                StepRunErrorCategory.Unknown),
        };
    }
}
