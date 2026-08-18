# Jumoo.TranslationManager.Automate — Implementation Plan

## Context

Umbraco.Automate is Umbraco's workflow automation product (triggers → conditions → actions). Translation Manager has no presence in it, so an editor cannot say "when this page is published, send it for translation" without writing code.

An earlier attempt exists at `D:\Source\Experiments\Automate\AutomateTests\Jumoo.TranslationManager.Automations` — one action forked from the AutoTranslate sample, built against `Umbraco.Automate 17.0.0-beta`, carrying a set of real defects. We are **starting clean** in the empty repo at `D:\Source\OpenSource\v17\Jumoo.TranslationManager.Automate` (branch `main`, no commits) and building against the current release.

Outcome: a shipped NuGet package, `Jumoo.TranslationManager.Automate`, contributing Translation Manager actions and triggers to Automate, structured and released the way `uSync.Automate` is.

**Agreed scope for v1:**
- Actions: Translate Content, Approve/Publish Job, Check Job Status
- Triggers: Job Submitted, Job Received, Job Approved, Job Published
- Target: v17 / `net10.0` only
- Content identified by a bindable `ContentKey` setting (`${trigger.contentKey}`), not by reading the trigger payload

**First step on approval:** copy this plan to `D:\Source\OpenSource\v17\Jumoo.TranslationManager.Automate\docs\implementation-plan.md` and commit it, so it lives with the repo.

---

## Verified findings

Everything below was checked against source or by reflecting the shipped assemblies — not assumed.

### Automate extensibility (`D:\Source\Experiments\Automate\Umbraco.Automate`, branch `v17/main`, v17.2.0)

- Reference **only `Umbraco.Automate.Core`**. Not the `Umbraco.Automate` meta-package (the prototype's mistake), not `.Startup`.
- Actions and triggers are **auto-discovered** by Umbraco's `TypeLoader` attribute scan (`Configuration/UmbracoBuilderExtensions.Collections.cs`). A composer is needed only for our own DI services.
- **No client-side code required.** Settings UI is generated server-side from `[Field]` attributes. `uSync.Automate.Actions` ships zero App_Plugins — so our build needs no npm step in any workflow.
- `[Field]` `Label`/`Description` must be set literally; omitted, they become localisation keys (`#uaFields_{model}{Prop}Label`), not humanised text.
- Required-ness is inferred from nullability. A non-nullable `Guid` can never be "unset", so `[Required]` on one is a no-op — this is why the prototype's `Guid.Empty` reached `GetProvider` and threw.
- `BackOfficeIdentityMiddleware` resolves the workspace service account and sets the ambient `IBackOfficeSecurityAccessor` for the duration of the step — but `ExecutionContext.ServiceAccountKey` is the authoritative source and is what we use.
- **`IContentScopedTriggerOutput` is `internal`** (`Dispatch/Authorization/IContentScopedTriggerOutput.cs`). Its own remarks say provider packages should declare their own marker. `ITriggerDispatchAuthorizer` *is* public and registerable via `builder.AutomateTriggerDispatchAuthorizers().Append<T>()`.
- `ActionResult` factories: `Success(output)`, `SuccessWithOutcome(outcome, output)`, `Skipped(reason)`, `Failed(ex, StepRunErrorCategory)`, `Sleep`, `WaitForInput`.

### Translation Manager API (reflected from `Jumoo.TranslationManager.Core` 17.7.1)

Integration contract: `[ComposeAfter(typeof(TranslationComposer))]` + resolve TM services from DI.

```
TranslationSetService.GetSetsByPathAsync(string path)
TranslationNodeService.CreateNodesAsync(TranslationSet, IContent, NodeCreationOptions, IEnumerable<TranslationSetSite>)
TranslationJobService.CreateJobAsync(name, nodes, provider, object options, IUser, JobOptions, groupId)
TranslationJobService.LoadJobNodesAsync(job) / SubmitJob(job) → Attempt<TranslationJob> / GetByKeyAsync / SaveAsync
ITranslationAprovalService.ApproveAsync(int jobId, TranslationJobApprovalOptions)   // TM spells it "Aproval", one 'p'
TranslationProviderCollection.GetProvider(Guid) → ITranslationProvider { Active(), Check/Submit/Cancel/Remove }
```

TM raises Umbraco `INotification`s in namespace `Jumoo.TranslationManager.Core` — **not** `.Notifications`:

```
TranslationJobNotification (base) { TranslationJob Job; bool Publish; Guid UserKey }
  ...Submitted / ...Received / ...Approved / ...Published / ...PartialApproval / ...ResetStatus / ...BulkStarting / ...BulkEnding
CancelableTranslationJobNotification : TranslationJobNotification
  ...Creating / ...Submitting / ...Approving / ...Publishing
TranslationNodeNotification (base) { TranslationNode Node } + Created / Approved / Published / Updated
```

**Four verified facts that shaped this plan:**

1. **Every TM method we call is non-virtual** — `GetSetsByPathAsync`, all four `CreateNodesAsync` overloads, `CreateJobAsync`, `SubmitJob`, `LoadJobNodesAsync`, `GetProvider` all report `IsVirtual=false` on concrete (non-interface) classes. **Moq cannot mock them.** A facade seam is therefore *mandatory*, not a nicety. `ITranslationAprovalService` is a real interface and mocks fine.
2. **TM has no pause/suppress hook** — regexing the assembly for `Pause|Paused|Suspend` returns nothing. All loop protection must be ours plus Automate's origin chain.
3. **`JobStatus` mixes statuses with sentinels**: `Created, Submitted, Returned, MAX_SUBMITTED, Partial, Received, PartialApproved, Processing, Reviewing, Accepted, Closed, Error, STATUSMAX, ARCHIVE_MAX`. `MAX_SUBMITTED`/`STATUSMAX`/`ARCHIVE_MAX` are comparison thresholds. Never `.ToString()` a raw status into an output.
4. **Culture fields**: `TranslationSetSite { Id, CultureName, CultureInfoView Culture }`, `CultureInfoView { Id, Name, DisplayName, ... }`. `CultureName`/`Culture.Name` are `fr-FR`; `DisplayName` is "French (France)".

Other verified: `TranslationChangeType { Existing, Create, Force }`; `NodeStatus { Open, InProgress, Updated, Reviewing, Approved, Rejected, Closed, Error, WorkingItem }`; `TranslationNode` carries `ContentVersionId`/`TargetVersionId`; TM's section alias string is `translation`.

### Lessons from the prototype

| Prototype defect | This plan |
|---|---|
| `JobOptions.AutoApprove = true` **and** an explicit `ApproveAsync` → double approval | `AutoApprove` is **always false**; approval is one explicit, synchronous path |
| Read `context.BindingData["trigger"]` directly | Bindable `ContentKey` setting — binding data may be an `OffloadedStepOutput` stand-in |
| `IBackOfficeSecurityAccessor` for identity, `ExecutionContext` never touched | `ExecutionContext.ServiceAccountKey` → `IUserService.GetAsync` |
| No `ICmsAction`, `RequiredSections`, or authorizer | All three, per `PublishContentAction` |
| Filtered sites on `Culture.DisplayName` — silently never matched `fr-FR` | Filter on `CultureName` / `Culture.Name`; regression test |
| `[Required]` on a non-nullable `Guid` (a no-op) | All keys are `string?` and parsed |
| Threw raw exceptions out of `ExecuteAsync` | Categorised `ActionResult.Failed` on every path |
| Hard-coded `TranslationChangeType.Force` | `Create` by default; `Force` is an explicit opt-in (see loop protection layer 4) |
| Empty output class | Rich, size-capped output DTOs |

---

## Repository layout

Modelled on `D:\Source\OpenSource\v17\uSync.Automate`.

```
Jumoo.TranslationManager.Automate.slnx
Directory.Build.props / Directory.Packages.props / GitVersion.yml / global.json
NuGet.Config / .gitignore / .gitattributes / .editorconfig
LICENSE (MPL-2.0) / readme.md / SECURITY.md / CODE_OF_CONDUCT.md
docs/implementation-plan.md              # this plan
assets/translation-manager-logo.png
umbraco-marketplace-jumoo.translationmanager.automate.json
dist/build-package.ps1
.github/{dependabot.yml, workflows/{dotnet-build,package-build,release,codeql}.yml}

Jumoo.TranslationManager.Automate/          # the package
Jumoo.TranslationManager.Automate.Tests/    # NUnit
Jumoo.TranslationManager.Automate.Site/     # dev site — see below
```

### Dev site

**Create it with the official template, do not hand-scaffold:**

```bash
dotnet new umbraco -n Jumoo.TranslationManager.Automate.Site --friendly-name "Admin" --email admin@example.com --development-database-type SQLite
```

Then add package references (`Umbraco.Automate`, `Jumoo.TranslationManager`) and a `ProjectReference` to the package project, set `IsPackable=false`, and copy the `Microsoft.ICU.ICU4C.Runtime` property block from `uSync.Automate.Site`. Everything else stays as the template generated it.

### Package project structure

```
Jumoo.TranslationManager.Automate/
  Actions/
    TranslateContentAction.cs      TranslateContentSettings.cs
    ApproveTranslationJobAction.cs ApproveTranslationJobSettings.cs
    CheckTranslationJobAction.cs   CheckTranslationJobSettings.cs
    Models/  TranslateContentOutput.cs  TranslationJobOutput.cs  TranslationJobSummary.cs
  Services/
    ITranslationManagerFacade.cs   TranslationManagerFacade.cs      # the mockable seam
    TranslationRunner.cs           TranslationRunRequest.cs  TranslationRunResult.cs
    ITranslationOperationGate.cs   TranslationOperationGate.cs
    ITranslationIdentityResolver.cs TranslationIdentityResolver.cs
    CsvParsing.cs
  Triggers/
    Jobs/  TranslationJobSubmittedTrigger.cs   TranslationJobReceivedTrigger.cs
           TranslationJobApprovedTrigger.cs    TranslationJobPublishedTrigger.cs
           TranslationJobTriggerOutput.cs      TranslationJobTriggerSettings.cs
    Mapping/ TranslationJobMapper.cs  TranslationJobTriggerFilter.cs
             TranslationIdempotency.cs TranslationOriginBehaviour.cs TranslationStatusMapper.cs
  Dispatch/
    ITranslationSetScopedOutput.cs  TranslationScopedDispatchAuthorizer.cs
  Configuration/
    TranslationManagerAutomateComposer.cs  TranslationManagerAutomateOptions.cs
    TranslationManagerAutomateConstants.cs
  readme.md
```

**Conventions.** Alias prefix `translationManager.` — precedent is the *product*, not the vendor (`umbracoAutomate.publishContent`, `slack.sendMessage`, `uSync.import`). UI `Group = "Translation Manager"`. Icons: `icon-globe` (translate), `icon-check` (approve), `icon-time` (check), `icon-inbox` (received), `icon-out` (submitted). **Aliases are persisted in saved automation JSON and cannot be renamed after release** — settle them at commit 1.

### Package references

The csproj mirrors `uSync.Automate.Actions.csproj`: plain `Microsoft.NET.Sdk`, `net10.0`, `GenerateDocumentationFile`, `PackageReadmeFile`, tag `umbraco-marketplace`, `InternalsVisibleTo` the test project, and exactly two `PackageReference`s — `Umbraco.Automate.Core` and `Jumoo.TranslationManager.Core`. No Razor SDK, no wwwroot, no npm.

**Pin TM Core at the floor version we support, not the newest.** NuGet's minimum-version resolution means referencing 17.7.1 forces every consumer onto ≥17.7.1. The six calls we make are identical across 17.4.0–17.7.1, so the package references **17.4.0**; the dev site pulls the current release. `Umbraco.Automate.Core` floors at **17.2.0** — that is where the extensibility surface stabilised.

Versions live in `Directory.Packages.props` (central package management): `Umbraco.Automate.Core` / `.Testing` 17.2.0, `Jumoo.TranslationManager.Core` 17.4.0, `Umbraco.Cms` (site) 17.6.1, `Jumoo.TranslationManager` (site) 17.7.1, `Umbraco.Automate` (site) 17.2.0, `Microsoft.SourceLink.GitHub`, NUnit 4.3.2 / NUnit3TestAdapter 6.2.0 / Moq 4.20.72 / coverlet.

---

## The facade seam (do this first — it is load-bearing)

Because TM's service methods are non-virtual on concrete classes, `TranslationRunner` cannot be unit-tested against them. Everything TM-shaped goes behind one interface:

```csharp
/// <summary>
/// The complete Translation Manager surface this package uses. Exists because TM's services
/// are concrete classes with non-virtual methods and therefore cannot be mocked directly.
/// This is also the single place the 17 -> 18 IContent -> IPublishableContentBase drift lands.
/// </summary>
public interface ITranslationManagerFacade
{
    Task<IEnumerable<TranslationSet>> GetSetsByPathAsync(string path);
    Task<IEnumerable<TranslationNode>> CreateNodesAsync(
        TranslationSet set, IContent content, NodeCreationOptions options, IEnumerable<TranslationSetSite> sites);
    Task<TranslationJob?> CreateJobAsync(
        string name, IEnumerable<TranslationNode> nodes, ITranslationProvider provider,
        object options, IUser user, JobOptions jobOptions, string groupId);
    Task<TranslationJob> LoadJobNodesAsync(TranslationJob job);
    Task<Attempt<TranslationJob>> SubmitJobAsync(TranslationJob job);   // TM's SubmitJob has no Async suffix
    Task<TranslationJob?> GetJobByKeyAsync(Guid key);
    Task<TranslationJob> SaveJobAsync(TranslationJob job);
    Task<bool> ApproveJobAsync(int jobId, TranslationJobApprovalOptions options);
    ITranslationProvider? GetProvider(Guid key);
}
```

`TranslationManagerFacade` is a pass-through with no logic. Every test mocks the facade; nothing mocks TM directly.

---

## Actions

### 1. `translationManager.translateContent` — "Translate Content"

Creates translation nodes for a content item across its matching sets and creates/submits one job per target culture.

| Field | Type | Notes |
|---|---|---|
| `ContentKey` | `string?` | `SupportsBindings`. "Use `${trigger.contentKey}` to translate the item that started this automation." |
| `Cultures` | `string?` | CSV of culture codes. Blank = every culture in the matching sets. Bindable |
| `SetKeys` | `string?` | CSV of set keys to restrict to. Blank = every set covering the item |
| `ProviderKey` | `string?` | Blank = the set's own connector, then the configured default |
| `JobNameTemplate` | `string?` | Bindable. Blank = `"{content} ({count}) to {culture}"` |
| `IncludeDescendants` | `bool` = false | Advanced |
| `Force` | `bool` = false | `TranslationChangeType.Force` vs `Create`. Advanced — see loop protection layer 4 |
| `Approve` | `bool` = false | Approve as soon as the connector returns. Advanced. "Only use with connectors that return instantly." |
| `Publish` | `bool` = false | Publish after approving; only applies with `Approve`. Advanced |
| `MaxJobDetails` | `int` = 20 | Output cap. Advanced |

All keys are `string?` and parsed — never a non-nullable `Guid`, which both defeats `[Required]` and blocks binding expressions.

**Output** `TranslateContentOutput`: `Submitted`, `Approved`, `JobCount`, `NodeCount`, `JobIds: int[]`, `JobKeys: Guid[]`, `Cultures: string[]`, `SetNames: string[]`, `ProviderKey`, `ProviderName`, `GroupId`, `Jobs: TranslationJobSummary[]` (capped, with `JobsTruncated`), `CompletedUtc`.

Flat arrays matter: `${translate.jobKeys}` feeds straight into `umbracoAutomate.forEach`. `Jobs` is capped for the same reason `uSyncRunOutput.ChangesTruncated` exists — output is persisted in the WorkflowCore run row and re-serialised on every binding evaluation.

```csharp
[Action("translationManager.translateContent", "Translate Content",
    Description = "Creates and submits Translation Manager jobs for a content item.",
    Group = "Translation Manager",
    Icon = "icon-globe",
    RequiredSections = [TranslationManagerAutomateConstants.SectionAlias],
    RequiredPermissions = [ActionUpdate.ActionLetter])]
public sealed class TranslateContentAction
    : ActionBase<TranslateContentSettings, TranslateContentOutput>, ICmsAction
{
    // ctor: ActionInfrastructure, TranslationRunner, IAutomationActionAuthorizer,
    //       IUmbracoContextFactory

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken ct)
    {
        var settings = context.GetSettings<TranslateContentSettings>();

        if (!Guid.TryParse(settings.ContentKey, out var contentKey) || contentKey == Guid.Empty)
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);

        if (await _authorizer.AuthorizeContentOrFailAsync(contentKey, RequiredPermissions, ct) is { } denied)
            return denied;

        if (context.ExecutionContext?.ServiceAccountKey is not { } serviceAccountKey)
            return ActionResult.Failed(
                new InvalidOperationException(
                    "No service account identity available. Translation jobs must be attributed to the workspace service account."),
                StepRunErrorCategory.Authentication);

        var request = new TranslationRunRequest { /* mapped settings */
            GroupId = context.RunId.ToString(),          // groups jobs by workflow run in TM's UI
            ServiceAccountKey = serviceAccountKey,
            PerformingDetails = context.ExecutionContext.FormatPerformingDetails(),
        };

        // The approve path writes and publishes content, raising notifications that resolve
        // URLs via UrlProvider. The outbox dispatcher has no HTTP scope, so there is no
        // ambient UmbracoContext without this.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _runner.RunAsync(request, ct);

        return result.Status switch
        {
            TranslationRunStatus.Success     => Success(result.Output!),
            TranslationRunStatus.NothingToDo => ActionResult.Skipped(result.Reason),
            TranslationRunStatus.Busy        => ActionResult.Skipped(result.Reason),
            TranslationRunStatus.Validation  => ActionResult.Failed(
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
```

Note "content is not in any translation set" maps to `Skipped`, not `Failed` — it is a normal outcome of a broad content trigger, not an error.

### 2. `translationManager.approveJob` — "Approve Translation Job"

Settings: `JobKey` (bindable), `Check` (`bool` = true, "Ask the connector for the latest translation before approving"), `Publish` (`bool` = true), `ApproveAllNodes` (`bool` = true), `NodeKeys` (`string?`, bindable, used when `ApproveAllNodes` is off).

`TranslationJobApprovalOptions.ApproveInBackground` is **hard-coded `false`** — see loop protection. Before approving, filter the job's master node keys through `IAutomationActionAuthorizer.FilterAuthorizedContentAsync`; approving nodes the service account cannot see would be privilege escalation.

`RequiredPermissions` is static on the attribute, so declare both `ActionUpdate` and `ActionPublish` and accept the slightly over-broad requirement; document it.

Output: `TranslationJobOutput`, refetched after approval.

### 3. `translationManager.checkJob` — "Check Translation Job"

Settings: `JobKey` (bindable), `UpdateStatus` (`bool` = true — "Contact the connector for the latest status. Turn off to read Translation Manager's stored status only").

Returns **`SuccessWithOutcome(status, output)`** so a workflow can branch on the outcome directly — this is what makes "translate → sleep → check → approve" buildable without an `if` step.

Output `TranslationJobOutput`: `Id`, `Key`, `Name`, `SetKey`, `SourceCulture`, `TargetCulture`, `Status` (mapped), `StatusChanged`, `IsComplete`, `IsError`, `NodeCount`, `ApprovedNodeCount`, `ProviderKey`, `ProviderName`, `ProviderStatus`, `Created`, `Submitted?`, `Received?`.

`IsComplete`/`IsError` are what workflow authors branch on — they hide the `MAX_SUBMITTED`/`STATUSMAX` sentinel mess. All status surfacing goes through `TranslationStatusMapper`, which never emits a sentinel.

### Deferred

`getJob` (read-only lookup), `createTranslationNodes` / `submitJob` (the split only matters for "create now, submit after human approval"), `cancelJob` (`Cancel`/`Remove` semantics vary per provider and need verification), `publishTranslation` (covered by `umbracoAutomate.publishContent` plus `approveJob`'s `Publish`).

---

## Triggers

**Only past-tense notifications become triggers.** The `-ing` ones are `Cancelable*`, and Automate's outbox dispatches asynchronously — a trigger cannot cancel the operation that raised it, so exposing them would be a UI lie. Say so in the readme.

| Alias | Notification | Name |
|---|---|---|
| `translationManager.jobSubmitted` | `TranslationJobSubmittedNotification` | Translation Job Submitted |
| `translationManager.jobReceived` | `TranslationJobReceivedNotification` | Translation Received |
| `translationManager.jobApproved` | `TranslationJobApprovedNotification` | Translation Job Approved |
| `translationManager.jobPublished` | `TranslationJobPublishedNotification` | Translation Published |

All four are `NotificationTriggerBase<TranslationJobTriggerSettings, TranslationJobTriggerOutput, TNotification>` sharing one settings class, one output and one mapper.

**`TranslationJobTriggerOutput`**: `JobId`, `JobKey`, `JobName`, `SetKey`, `SetName?`, `SourceCulture`, `TargetCulture`, `NodeCount`, `ApprovedNodeCount`, `Status` (mapped), `IsError`, `ProviderKey`, `ProviderName?`, `GroupId?`, `UserKey`, `Publish`, `ContentKeys: Guid[]` (capped, default 100).

`ContentKeys` is what lets a workflow do `forEach` → `publishContent`. `TranslationJob.Nodes` is empty until `LoadJobNodesAsync`, and `MapEvent` runs on the notification thread — so the mapper must tolerate an empty list and emit `ContentKeys = []` rather than calling a service.

> **Trigger output is a compatibility boundary.** Once an automation binds `${trigger.targetCulture}`, renaming that property silently breaks live workflows. Get this shape right in phase 7.

**`TranslationJobTriggerSettings`**: `SetKeys` (CSV), `Cultures` (CSV), `ProviderKeys` (CSV), `IgnoreErrors` (`bool` = true), and `OnAutomationOriginatedEvent` — a dropdown (`Run`/`SkipOnCycle`/`SkipAlways`, default `SkipOnCycle`, Group `Advanced`) with `IAutomationOriginatedEventBehavior` implemented explicitly, exactly as `uSyncBulkTriggerSettings` does. `SkipOnCycle` rather than `SkipAlways` is right here: an "approve when received" chain is legitimate machine-driven work and TM job events are low-volume.

Culture comparison uses `CultureName` / `Culture.Name`, never `DisplayName`. Put a comment on it — the prototype's `DisplayName` match reads as correct and silently never fired.

### Idempotency

Job notifications carry no version id (unlike node ones, which have `TargetVersionId`), so:

```csharp
// {alias}:{jobKey}:s{status}:t{ticks} — the timestamp separates a genuine re-run after a
// status reset from a duplicate notification for one event.
ForJobEvent(alias, job, stamp)     => $"{alias}:{job.Key}:s{(int)job.Status}:t{stamp?.Ticks ?? 0}";

// {alias}:{jobKey}:s{status}:n{approvedCount} — approval and publish carry no timestamp.
// Approved-node count is monotonic within a job, so it distinguishes a partial from a full
// approval while collapsing duplicates of the same approval.
ForJobProgress(alias, job, approvedCount) => $"{alias}:{job.Key}:s{(int)job.Status}:n{approvedCount}";
```

- `jobSubmitted` → `ForJobEvent(alias, job, job.Submitted)`
- `jobReceived` → `ForJobEvent(alias, job, job.Received)`
- `jobApproved` / `jobPublished` → `ForJobProgress(alias, job, approvedNodeCount)`

Accepted consequence, to be documented: approving the same job twice with the same approved-node count dedupes to one event. That is desirable — it is exactly the double-approval class we are guarding against.

### Dispatch authorization

`IContentScopedTriggerOutput` is internal, and a translation job isn't scoped to a single node anyway, so we declare our own marker `ITranslationSetScopedOutput { Guid GetSetKey(); }` on the output and register `TranslationScopedDispatchAuthorizer` via `builder.AutomateTriggerDispatchAuthorizers().Append<T>()`. For v1 it returns `Success` (which per the interface docs means "not blocking", not "approved") — the section guard has already run. The point is that the marker is on the output from day one; retrofitting one is a breaking schema change.

---

## Shared service layer: `TranslationRunner`

Adapted from the proven `AutomaticTranslationService` flow, with identity, gating, options and result-shaping lifted out. Depends on `ITranslationManagerFacade`, never on TM directly. Actions become argument-mapping shells.

```csharp
public enum TranslationRunStatus { Success, NothingToDo, Busy, Validation, ProviderUnavailable, Failed }
```

`RunAsync` flow:

1. **Acquire the gate** (`TryAcquireAsync`, `MaxWaitSeconds`). Failure → `Busy` → `Skipped`. Two concurrent runs over overlapping content create duplicate open nodes a human then has to reconcile, so refuse rather than queue.
2. `IContentService.GetById(contentKey)` → `Validation` if missing. Expand descendants if requested.
3. `facade.GetSetsByPathAsync(item.Path)` per item, `DistinctBy(Id)`, filter by `SetKeys` and by `options.ExcludedSets`. Empty → `NothingToDo`.
4. Resolve identity: `ITranslationIdentityResolver.ResolveAsync(serviceAccountKey)` → `IUser` (`CreateJobAsync` needs an `IUser`, not a key). Null → `Validation`.
5. Per set — provider precedence `request.ProviderKey ?? set.ProviderKey ?? options.DefaultProviderKey`; `Validation` if unresolved, `ProviderUnavailable` if `!provider.Active()`.
6. `sites = set.Sites.Where(s => cultures is null || cultures.Contains(s.CultureName, OrdinalIgnoreCase))` — **`CultureName`, not `DisplayName`**.
7. `facade.CreateNodesAsync(set, item, new NodeCreationOptions { ChangeType = Force ? Force : Create, DefaultNodeStatus = NodeStatus.Open, IncludeNameChange = true }, sites)`.
8. Group nodes by `node.Culture.Name` → one job per target culture. `CreateJobAsync(..., new JobOptions { AutoApprove = false }, groupId)` — **always false**, see below.
9. `LoadJobNodesAsync(job)` **before** `SubmitJobAsync(job)` — nodes are not loaded on the returned job, and submitting without this sends an empty job. Check `Attempt.Success`; a failed attempt surfaces as `Failed` with the attempt's exception preserved.
10. If `request.Approve`, approve **synchronously here** with `ApproveInBackground = false`.
11. Build the capped output.

Sibling methods `ApproveJobAsync` / `CheckJobAsync` serve the other two actions, so identity, gating and mapping are shared and each action stays under ~50 lines.

`ITranslationIdentityResolver` wraps `IUserService.GetAsync(serviceAccountKey)` with a `ConcurrentDictionary` cache. An optional super-user fallback is gated behind `AllowSuperUserFallback`, **default false** — silently attributing translations to the super user is an audit hole.

---

## Loop protection

The cascade to prevent: `contentPublished` → `translateContent` → TM approves and publishes translated content → `ContentPublishedNotification` → `contentPublished` fires again → same automation → loop. At a paid MT provider this costs real money, so this is the highest-severity risk in the plan.

**Layer 1 — Automate's origin chain (primary, and free).** Automate's middleware pushes the ambient origin for the duration of the run; events raised *inside* the run carry `OriginChain ∪ { thisAutomationId }`, and a trigger set to `SkipOnCycle` will not re-enter its own automation. **This only works if the content writes happen synchronously on the action's thread.** Hence three hard architectural rules:

- `JobOptions.AutoApprove` is **always `false`**. TM's `AutoApproverNotificationHandler` runs on a background task when a job is Received — outside the ambient origin. The prototype set it true *and* called `ApproveAsync`, which both double-approved and blew a hole in loop protection.
- `TranslationJobApprovalOptions.ApproveInBackground` is **always `false`** — it also makes the step report success before the work has happened.
- The runner never uses `Task.Run`, `IBackgroundTaskQueue` or a `HostedService`.

If a user wants TM's own background auto-approve, they configure it on the *set* in TM. The readme must state that content published that way is not origin-tagged, so their content triggers need a content-type or path filter instead.

**Layer 2 — `IAutomationOriginatedEventBehavior`** on every trigger settings class, default `SkipOnCycle`.

**Layer 3 — `ITranslationOperationGate`**, a port of `uSyncOperationGate` (`SemaphoreSlim` + disposable lease). Registered `TryAddSingleton` so a future companion package contends for the same lock. It exposes `IsRunning`, consulted in our own triggers' `MapEvent` (behind `SuppressSelfOriginatedEvents`, default true) so `approveJob` doesn't immediately re-fire `jobApproved` into the same chain.

**Layer 4 — TM's own change detection.** Defaulting `ChangeType` to `Create` rather than `Force` means a re-entrant translate of unchanged content produces zero nodes and returns `Skipped`. The prototype hard-coded `Force`, turning every accidental re-entry into billable provider traffic. `Force` is an explicit opt-in toggle.

---

## Identity & authorization

| Concern | Approach |
|---|---|
| Who is the run | `context.ExecutionContext.ServiceAccountKey`, authoritative. Not the security accessor — a translation job attributed to whoever happened to be logged in is worse than one attributed to the service account |
| `IUser` for `CreateJobAsync` | `ITranslationIdentityResolver` → `IUserService.GetAsync`, cached. Unresolvable → `Authentication` failure; super-user fallback config-gated, off by default |
| Audit | `ICmsAction` on every write action + `FormatPerformingDetails()` through the request for logging |
| Section | `RequiredSections = [TranslationManagerAutomateConstants.SectionAlias]` — our own const, value `"translation"`. **Verify against TM's registered section alias in phase 2**; a wrong value silently blocks all dispatch |
| Node permissions | `ActionUpdate` on translate; `ActionUpdate` + `ActionPublish` on approve |
| Per-node check | `IAutomationActionAuthorizer.AuthorizeContentOrFailAsync` at the top of `translateContent` |
| Descendants | Authorise the **root only** — start-node semantics already imply the subtree, and per-descendant checks on a large tree would be pathological. Document |
| Job actions | Cannot authorise one node up front (a job spans many). `approveJob` filters the job's master node keys via `FilterAuthorizedContentAsync` before approving |

---

## Configuration

```csharp
public class TranslationManagerAutomateOptions
{
    public const string Section = "TranslationManager:Automate";

    public Guid? DefaultProviderKey { get; set; }
    public int MaxWaitSeconds { get; set; }                    // 0 = fail fast if another run holds the gate
    public int MaxContentKeysPerEvent { get; set; } = 100;     // trigger output cap
    public bool SuppressSelfOriginatedEvents { get; set; } = true;
    public bool AllowSuperUserFallback { get; set; }           // off: hiding who did the work is an audit hole
    public Guid[] ExcludedSets { get; set; } = [];
}
```

Composer — the entire DI surface, since actions and triggers register themselves:

```csharp
[ComposeAfter(typeof(TranslationComposer))]   // the runner resolves TM's services from DI
public class TranslationManagerAutomateComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AddTranslationManagerAutomate();
}

public static class TranslationManagerAutomateBuilderExtensions
{
    public static IUmbracoBuilder AddTranslationManagerAutomate(this IUmbracoBuilder builder)
    {
        if (builder.IsUmbracoBackOfficeEnabled() is false) return builder;

        builder.Services.TryAddSingleton<ITranslationManagerFacade, TranslationManagerFacade>();
        builder.Services.TryAddSingleton<ITranslationOperationGate, TranslationOperationGate>();
        builder.Services.TryAddSingleton<ITranslationIdentityResolver, TranslationIdentityResolver>();
        builder.Services.AddSingleton<TranslationRunner>();

        builder.AutomateTriggerDispatchAuthorizers().Add<TranslationScopedDispatchAuthorizer>();

        builder.Services.AddOptions<TranslationManagerAutomateOptions>()
            .Bind(builder.Config.GetSection(TranslationManagerAutomateOptions.Section));

        return builder;
    }
}
```

No `[ComposeAfter]` on any Automate composer — `UmbracoAutomateComposer` lives in `Umbraco.Automate.Startup`, which we deliberately do not reference.

---

## Testing

`Jumoo.TranslationManager.Automate.Tests` — NUnit 4 + Moq + `Umbraco.Automate.Testing`, matching `uSync.Automate.Tests.csproj` (`IsPackable=false`, `IsTestProject=true`), wired by `InternalsVisibleTo`.

Mocks: `ITranslationManagerFacade`, `IContentService`, `ITranslationIdentityResolver`, `IAutomationActionAuthorizer`, `IUmbracoContextFactory`, `IOptionsMonitor<TranslationManagerAutomateOptions>`, `ITranslationOperationGate`. Build a shared test builder that registers every ctor dependency — `ActionTestHarness` uses `ActivatorUtilities.CreateInstance`, so a missing `WithService` fails at construction with a DI error rather than a readable assertion.

**Do not copy assertions from the `dotnet new umbraco-automate-actions` template test** — it asserts `ActionStatus.Succeeded` and indexes `OutputData` as a dictionary. The real API is `ActionResultStatus` and `object? OutputData`.

Representative tests:

*Actions (harness):* missing/non-GUID `ContentKey` → `Failed`/`Validation`; null `ExecutionContext` → `Failed`/`Authentication`; authorizer denies → `Failed` and the runner is never called; happy path → `Success` with a typed output; `checkJob` sets the outcome to the mapped status.

*Runner:* content in no set → `NothingToDo`; gate held → `Busy`; provider precedence (setting > set > default); inactive provider → `ProviderUnavailable`; 3 cultures ⇒ 3 `CreateJobAsync` calls sharing one `groupId`; **`LoadJobNodesAsync` called before every `SubmitJobAsync`** (empty-job trap); **`JobOptions.AutoApprove` false on every call** (double-approval); **`ApproveInBackground` false** (lost origin chain); `Force=false` ⇒ `ChangeType.Create`; **culture filter matches `CultureName` and a `DisplayName` value does not** (silent-filter bug); failed `SubmitJob` attempt → `Failed` with the exception preserved; `MaxJobDetails` caps `Jobs` and sets `JobsTruncated`; null identity + `AllowSuperUserFallback=false` → `Validation`.

*Triggers:* null `Job` yields nothing; idempotency key stable across duplicate notifications and different after reset+resubmit; `jobApproved` key differs between a 2-node and 4-node approval of the same job; `CanHandle` filters on set/culture/provider/`IgnoreErrors`; `ContentKeys` capped; `IsRunning` suppresses when `SuppressSelfOriginatedEvents` is on.

*Mapping:* `TranslationStatusMapper` never emits `MAX_SUBMITTED`/`STATUSMAX`/`ARCHIVE_MAX`, and `IsComplete` is true for `Received`/`Accepted`/`Closed`; `TranslationOriginBehaviour.Parse` round-trips and falls back on garbage.

*Services:* gate second-acquire returns null and releases on dispose; identity resolver caches.

**Loop protection cannot be unit-tested** — the origin chain is Automate infrastructure. The dev-site smoke test in phase 8 is the real verification and is worth more than any of the above for the first release.

---

## CI/CD & packaging

Copy `uSync.Automate`'s four workflows with these edits:

- **`dotnet-build.yml` / `package-build.yml`** — point at `./Jumoo.TranslationManager.Automate.slnx`, one test project, one `dotnet pack`. **Drop every npm step** — we ship no client bundle.
- **`release.yml`** — keep everything: tag-shape check, the "tag is on `main` or `vN/main`" containment check, `environment: nuget`, `id-token: write`, `NuGet/login@v1` trusted publishing, `--skip-duplicate`, and the two Windows/pwsh quirks its comments document. **Manual prerequisite:** create the nuget.org trusted-publishing policy for this repo + `release.yml` + the `nuget` environment before the first tag.
- **`codeql.yml`** — enable when the repo goes public.

`GitVersion.yml`: `next-version: 17.0.0`, `mode: ContinuousDeployment`. `dist/build-package.ps1` adapted from uSync's.

Marketplace json: `Category` matched to Translation Manager's own listing, `Tags: ["automate","workflow","automation","translation","localization"]`, `RelatedPackages` naming `Jumoo.TranslationManager` and `Jumoo.TranslationManager.AI`.

Branching: `main` for the v17 line; cut `v18/main` when TM Core 18.x becomes primary. **Prefer a branch per major over `#if` multi-targeting** — the only drift is `CreateNodesAsync`'s `IContent` → `IPublishableContentBase`, and it is confined to the facade.

---

## Implementation phases

Each phase builds, tests green, and is one commit.

1. **Scaffold the repo.** `.gitignore`, `.gitattributes`, `.editorconfig`, LICENSE, `global.json`, `NuGet.Config`, `GitVersion.yml`, `Directory.Build.props`, `Directory.Packages.props`, `assets/`, root `readme.md`, and `docs/implementation-plan.md` (this file). *Verify: `dotnet --version` honours global.json.*
2. **Package project + solution + constants.** csproj referencing only `Umbraco.Automate.Core` and `Jumoo.TranslationManager.Core`, packed readme, `.slnx`. Confirm TM's real section alias here. *Verify: `dotnet pack` produces a nupkg with the right id, icon and readme.*
3. **Dev site** via `dotnet new umbraco` (command above), plus package refs and the project reference. *Verify: site boots, Automate and Translation Manager sections both present.*
4. **Configuration, options, composer, gate, identity resolver, facade.** *Verify: site boots with the composer registered; options bind from `appsettings.json`.*
5. **`TranslationRunner` create/submit path.** No actions yet. *Verify: runner unit tests pass.*
6. **`translateContent`** — action, settings, output, `CsvParsing`. *Verify: harness tests; in the dev site a manual-trigger automation creates and submits a real job.*
7. **`checkJob` + `approveJob` + `TranslationStatusMapper`.** *Verify: status-mapper tests; dev-site translate → check → approve puts translated content back and terminates.*
8. **Triggers** — output, settings, mapper, idempotency, filter, the four trigger classes, marker + dispatch authorizer. *Verify: trigger tests; a dev-site automation started by `jobReceived`.*
9. **Loop-protection hardening.** Wire `IsRunning` into `MapEvent`; build the `contentPublished → translateContent` automation and confirm it terminates on its own. *Verify: manual — the single most important check in this list; run it with a provider quota alarm in place.*
10. **Docs.** Package readme (aliases, settings, binding examples, the "TM background auto-approve is not origin-tagged" warning, the "-ing notifications are not triggers" rationale), marketplace json, `appsettings-schema` file.
11. **CI/CD.** Four workflows, `dependabot.yml`, `dist/build-package.ps1`, nuget.org trusted-publishing policy. *Verify: PR build green; push to main produces an artifact; tag `v17.0.0-beta` publishes.*

---

## Verification

- `dotnet build Jumoo.TranslationManager.Automate.slnx` and `dotnet test` from the repo root.
- Dev site: create a translation set with two target cultures and the Passthrough provider, then build **Content Published → Translate Content → (Job Received trigger) → Check Job → Approve Job** and confirm it runs and terminates.
- In the Automate UI: every action/trigger shows the right name, group, icon and generated fields; `${trigger.contentKey}` binds into `ContentKey`; `checkJob`'s outcome is selectable as a branch.
- In the TM dashboard: one job per target culture, submitted, approved exactly once, grouped by the Automate run id.
- In the Automate run log: correct step outputs and sensible `StepRunErrorCategory` values on failures.

---

## Risks and open questions

**Needs an answer before phase 5:**

1. **`TranslationChangeType` semantics.** Is `Create` the right default, or does it skip content that *has* changed since the last translation, making `Force` necessary? Both existing implementations hard-code `Force`, which may be masking this. It determines whether the default costs users money on every re-run.
2. **Does `AutoApprove = true` plus an explicit `ApproveAsync` actually double-apply?** The mechanism (`Tasks.AutoApproverNotificationHandler`) is confirmed present in the assembly; the failure mode should be observed rather than assumed.
3. **Does `ApproveInBackground = true` lose the ambient automation origin?** The plan assumes yes and hard-codes `false`. If TM's background queue flows `AsyncLocal`, the constraint relaxes — but `false` remains right for step-completion semantics.
4. **TM's section alias.** Our constant says `"translation"` (found in the assembly next to `SectionAlias`). Confirm in phase 2 — a wrong value silently blocks all dispatch.
5. **Set-level `AutoSend` / `AlwaysSend` / `AutoApprove`.** These exist on `TranslationSet` and interact with everything our actions do. Should `translateContent` respect them, override them, or refuse to run against a set with `AutoSend` on (double submission)?

**Awkward TM API shapes, handled:** `ITranslationAprovalService` is misspelled (one `p`) — use as-is, comment so nobody "fixes" it. `SubmitJob` lacks an `Async` suffix but returns a `Task` — normalised in the facade. `ApproveAsync` returns a bare `bool`, so a failure message can only point at the TM log — **worth asking the TM team for an `Attempt<>` or status enum**. `CreateJobAsync` takes an untyped `object options` for provider settings (we pass `new object()`; ask what it's for) and needs an `IUser` rather than a key — **worth asking for a key-based overload**. TM's async API has no `CancellationToken` anywhere, so cancellation is only observable between sets and jobs — the same constraint `uSyncRunner` documents.

**Other risks:** `Umbraco.Automate.Core` 17.2.0 is young and already has internal types we wanted (`IdempotencyKeyFactory`, `IContentScopedTriggerOutput`) — pin exactly and re-verify on every Automate bump. Aliases and trigger output property names are permanent once released.

**Deliberately not in v1:** node-level triggers (high volume — would need a budget like `uSyncItemTriggerBudget`; the recommended pattern is a job trigger plus `forEach` over its `ContentKeys`); `getJob`, `cancelJob`, split create/submit actions; a custom set/connector picker property editor (would pull in an App_Plugins bundle, a management API endpoint and npm steps in three workflows — v1 uses GUID text fields, which is the one real UX compromise here).
