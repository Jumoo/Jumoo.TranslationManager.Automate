using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Security;

namespace Jumoo.TranslationManager.Automate.Dispatch;

/// <summary>
/// Authorises dispatch of this package's triggers. A translation job is not scoped to a
/// single content node the way Automate's built-in <c>NodeScopedTriggerDispatchAuthorizer</c>
/// expects, so it cannot be reused here - <see cref="ITranslationSetScopedOutput"/> exists so
/// a future release can gate dispatch on per-set permissions once Translation Manager has
/// them. For now this authoriser is a no-op: Automate's section guard (RequiredSections) has
/// already run by the time authorisers are consulted, and returning Success means "not
/// blocking", not "explicitly approved" - so declaring it now, rather than retrofitting the
/// marker interface onto shipped trigger outputs later, avoids a breaking schema change.
/// </summary>
public sealed class TranslationScopedDispatchAuthorizer : ITriggerDispatchAuthorizer
{
    public Task<AutomationAuthorizationResult> AuthorizeAsync(
        TriggerDispatchAuthorizationContext context, CancellationToken cancellationToken)
        => Task.FromResult(AutomationAuthorizationResult.Success);
}
