using Jumoo.TranslationManager.Automate.Actions.Models;
using Jumoo.TranslationManager.Automate.Configuration;
using Jumoo.TranslationManager.Automate.Mapping;
using Jumoo.TranslationManager.Core.Models;
using Jumoo.TranslationManager.Core.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>
/// Orchestrates Translation Manager operations behind <see cref="ITranslationManagerFacade"/>,
/// adapted from the proven set-&gt;nodes-&gt;jobs-&gt;submit flow in
/// Jumoo.TranslationManager.AutoTranslate's AutomaticTranslationService, with identity, gating,
/// options and result-shaping lifted out so actions stay thin argument-mapping shells.
///
/// <c>JobOptions.AutoApprove</c> is always <c>false</c> and approval (when requested) always
/// happens synchronously in this method with <c>ApproveInBackground = false</c>. Both of
/// Translation Manager's background paths run outside Umbraco Automate's ambient automation
/// origin, which defeats Automate's SkipOnCycle loop guard on any content-published trigger
/// chained after this action - see the package readme for the full loop-protection rationale.
/// </summary>
public sealed class TranslationRunner
{
    private readonly ITranslationManagerFacade _facade;
    private readonly ITranslationOperationGate _gate;
    private readonly ITranslationIdentityResolver _identity;
    private readonly IContentService _contentService;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IOptionsMonitor<TranslationManagerAutomateOptions> _options;
    private readonly ILogger<TranslationRunner> _logger;

    public TranslationRunner(
        ITranslationManagerFacade facade,
        ITranslationOperationGate gate,
        ITranslationIdentityResolver identity,
        IContentService contentService,
        IAutomationActionAuthorizer authorizer,
        IOptionsMonitor<TranslationManagerAutomateOptions> options,
        ILogger<TranslationRunner> logger)
    {
        _facade = facade;
        _gate = gate;
        _identity = identity;
        _contentService = contentService;
        _authorizer = authorizer;
        _options = options;
        _logger = logger;
    }

    public async Task<TranslationRunResult> RunAsync(TranslationRunRequest request, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;

        // Fail fast rather than queue: a translate run is expensive, and a second concurrent
        // run against overlapping content creates duplicate open translation nodes that a
        // human then has to reconcile.
        using var lease = await _gate.TryAcquireAsync(TimeSpan.FromSeconds(options.MaxWaitSeconds), cancellationToken);
        if (lease is null)
            return TranslationRunResult.Busy("Another translation run is already in progress.");

        try
        {
            var content = _contentService.GetById(request.ContentKey);
            if (content is null)
                return TranslationRunResult.Invalid($"Content '{request.ContentKey}' was not found.");

            var items = request.IncludeDescendants
                ? new[] { content }.Concat(GetDescendants(content.Id)).ToList()
                : [content];

            var sets = new List<TranslationSet>();
            foreach (var item in items)
                sets.AddRange(await _facade.GetSetsByPathAsync(item.Path));

            sets = sets
                .DistinctBy(s => s.Id)
                .Where(s => request.SetKeys is null || request.SetKeys.Contains(s.Key))
                .Where(s => !options.ExcludedSets.Contains(s.Key))
                .ToList();

            if (sets.Count == 0)
                return TranslationRunResult.Nothing($"Content '{content.Name}' is not inside any translation set.");

            var user = await _identity.ResolveAsync(request.ServiceAccountKey, cancellationToken);
            if (user is null)
                return TranslationRunResult.Invalid(
                    $"Could not resolve a backoffice user for service account '{request.ServiceAccountKey}'.");

            var createdJobs = new List<TranslationJob>();
            ITranslationProvider? usedProvider = null;

            foreach (var set in sets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Provider precedence: explicit setting > the set's own connector > config default.
                var providerKey = request.ProviderKey ?? set.ProviderKey ?? options.DefaultProviderKey;
                if (providerKey is null || providerKey == Guid.Empty)
                    return TranslationRunResult.Invalid($"No translation connector configured for set '{set.Name}'.");

                var provider = _facade.GetProvider(providerKey.Value);
                if (provider is null)
                    return TranslationRunResult.Invalid($"Translation connector '{providerKey}' was not found.");
                if (!provider.Active())
                    return TranslationRunResult.ProviderUnavailable($"Translation connector '{provider.Name}' is not active.");

                usedProvider = provider;

                var nodeOptions = new NodeCreationOptions(
                    request.Force ? TranslationChangeType.Force : TranslationChangeType.Create,
                    includeNameChange: true,
                    defaultStatus: NodeStatus.Open);

                // Culture filtering matches CultureName / Culture.Name, NEVER Culture.DisplayName
                // (e.g. "French (France)") - the latter never matches a configured culture code
                // like "fr-FR".
                var sites = set.Sites.Where(s =>
                    request.Cultures is null ||
                    request.Cultures.Contains(s.CultureName, StringComparer.OrdinalIgnoreCase));

                var nodes = new List<TranslationNode>();
                foreach (var item in items)
                    nodes.AddRange(await _facade.CreateNodesAsync(set, item, nodeOptions, sites));

                if (nodes.Count == 0) continue;

                foreach (var group in nodes.GroupBy(n => n.Culture.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var groupNodes = group.ToList();
                    var name = FormatJobName(request.JobNameTemplate, content.Name, groupNodes.Count, group.Key);

                    // AutoApprove is deliberately false - see the class remarks.
                    var jobOptions = new JobOptions { AutoApprove = false };

                    var job = await _facade.CreateJobAsync(
                        name, groupNodes, provider, new object(), user, jobOptions, request.GroupId);
                    if (job is null)
                    {
                        _logger.LogWarning("Translation Manager returned no job for '{Name}' - skipped.", name);
                        continue;
                    }

                    // Nodes are not loaded on the job CreateJobAsync returns - submitting
                    // without this sends an empty job to the connector.
                    job = await _facade.LoadJobNodesAsync(job);
                    if (job is null)
                        return TranslationRunResult.Fail($"Failed to load nodes for translation job '{name}'.");

                    var submitted = await _facade.SubmitJobAsync(job);
                    if (!submitted.Success || submitted.Result is null)
                        return TranslationRunResult.Fail(
                            $"Failed to submit translation job '{name}'.", submitted.Exception);

                    createdJobs.Add(submitted.Result);
                }
            }

            if (createdJobs.Count == 0)
                return TranslationRunResult.Nothing("Translation Manager found nothing to translate.");

            var approved = false;
            if (request.Approve)
                approved = await ApproveCreatedJobsAsync(createdJobs, request, user, cancellationToken);

            return TranslationRunResult.Ok(BuildOutput(createdJobs, usedProvider!, request, approved));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TranslationRunResult.Fail("Cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation run for content {ContentKey} failed", request.ContentKey);
            return TranslationRunResult.Fail(ex.Message, ex);
        }
    }

    private async Task<bool> ApproveCreatedJobsAsync(
        List<TranslationJob> jobs,
        TranslationRunRequest request,
        Umbraco.Cms.Core.Models.Membership.IUser user,
        CancellationToken cancellationToken)
    {
        var all = true;
        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ApproveInBackground is deliberately false - see the class remarks.
            var ok = await _facade.ApproveJobAsync(job.Id, new TranslationJobApprovalOptions
            {
                UserKey = user.Key,
                Approve = true,
                ApproveAllNodes = true,
                Check = true,
                Publish = request.Publish,
                ApproveInBackground = false,
            });

            if (!ok)
            {
                _logger.LogWarning("Translation Manager refused to approve job {JobId} ({JobName})", job.Id, job.Name);
                all = false;
            }
        }

        return all;
    }

    private static TranslateContentOutput BuildOutput(
        List<TranslationJob> jobs, ITranslationProvider provider, TranslationRunRequest request, bool approved)
    {
        var truncated = jobs.Count > request.MaxJobDetails;
        var summaries = jobs
            .Take(request.MaxJobDetails)
            .Select(j => new TranslationJobSummary
            {
                Id = j.Id,
                Key = j.Key,
                Name = j.Name,
                SetKey = j.SetKey,
                SourceCulture = j.SourceCulture?.Name ?? string.Empty,
                TargetCulture = j.TargetCulture?.Name ?? string.Empty,
                NodeCount = j.NodeCount,
                Status = TranslationStatusMapper.ToName(j.Status),
            })
            .ToArray();

        return new TranslateContentOutput
        {
            Submitted = true,
            Approved = approved,
            JobCount = jobs.Count,
            NodeCount = jobs.Sum(j => j.NodeCount),
            JobIds = jobs.Select(j => j.Id).ToArray(),
            JobKeys = jobs.Select(j => j.Key).ToArray(),
            Cultures = jobs.Select(j => j.TargetCulture?.Name ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            SetNames = jobs.Select(j => j.SetKey.ToString()).Distinct().ToArray(),
            ProviderKey = provider.Key,
            ProviderName = provider.Name,
            GroupId = request.GroupId,
            Jobs = summaries,
            JobsTruncated = truncated,
            CompletedUtc = DateTime.UtcNow,
        };
    }

    public async Task<TranslationJobRunResult> CheckJobAsync(CheckJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _facade.GetJobByKeyAsync(request.JobKey);
            if (job is null)
                return TranslationJobRunResult.Invalid($"Translation job '{request.JobKey}' was not found.");

            var previousStatus = job.Status;
            var statusChanged = false;

            if (request.UpdateStatus)
            {
                var provider = _facade.GetProvider(job.ProviderKey);
                if (provider is null)
                    return TranslationJobRunResult.Invalid($"Translation connector '{job.ProviderKey}' was not found.");

                var checkResult = await provider.Check(job);
                if (!checkResult.Success)
                    return TranslationJobRunResult.Fail(
                        $"Failed to check translation job '{job.Name}'.", checkResult.Exception);

                job = checkResult.Result ?? job;
                statusChanged = job.Status != previousStatus;

                if (statusChanged)
                    job = await _facade.SaveJobAsync(job) ?? job;
            }

            return TranslationJobRunResult.Ok(BuildJobOutput(job, statusChanged, approved: false, published: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checking translation job {JobKey} failed", request.JobKey);
            return TranslationJobRunResult.Fail(ex.Message, ex);
        }
    }

    public async Task<TranslationJobRunResult> ApproveJobAsync(ApproveJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _facade.GetJobByKeyAsync(request.JobKey);
            if (job is null)
                return TranslationJobRunResult.Invalid($"Translation job '{request.JobKey}' was not found.");

            var user = await _identity.ResolveAsync(request.ServiceAccountKey, cancellationToken);
            if (user is null)
                return TranslationJobRunResult.Invalid(
                    $"Could not resolve a backoffice user for service account '{request.ServiceAccountKey}'.");

            // Approving nodes the service account cannot see would be a privilege escalation -
            // fail closed if any node's master content is outside the account's authorized set.
            job = await _facade.LoadJobNodesAsync(job) ?? job;
            var authFailure = await CheckNodeAuthorizationAsync(job, cancellationToken);
            if (authFailure is not null)
                return authFailure;

            // ApproveInBackground is deliberately false - see the class remarks on RunAsync.
            var approved = await _facade.ApproveJobAsync(job.Id, new TranslationJobApprovalOptions
            {
                UserKey = user.Key,
                Approve = true,
                ApproveAllNodes = request.ApproveAllNodes,
                NodeKeys = request.ApproveAllNodes ? [] : request.NodeKeys ?? [],
                Check = request.Check,
                Publish = request.Publish,
                ApproveInBackground = false,
            });

            if (!approved)
                return TranslationJobRunResult.Fail($"Translation Manager refused to approve job '{job.Name}'.");

            var refreshed = await _facade.GetJobByKeyAsync(job.Key);
            if (refreshed is not null)
                refreshed = await _facade.LoadJobNodesAsync(refreshed);

            return TranslationJobRunResult.Ok(
                BuildJobOutput(refreshed ?? job, statusChanged: true, approved: true, published: request.Publish));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approving translation job {JobKey} failed", request.JobKey);
            return TranslationJobRunResult.Fail(ex.Message, ex);
        }
    }

    private async Task<TranslationJobRunResult?> CheckNodeAuthorizationAsync(TranslationJob job, CancellationToken cancellationToken)
    {
        var masterNodeIds = job.Nodes.Select(n => n.MasterNodeId).Distinct().ToList();
        if (masterNodeIds.Count == 0)
            return null;

        var contentKeys = new List<Guid>();
        foreach (var id in masterNodeIds)
        {
            var content = _contentService.GetById(id);
            if (content is not null)
                contentKeys.Add(content.Key);
        }

        if (contentKeys.Count == 0)
            return null;

        var authorized = await _authorizer.FilterAuthorizedContentAsync(
            contentKeys, new HashSet<string> { ActionUpdate.ActionLetter }, cancellationToken);

        return authorized.Count < contentKeys.Count
            ? TranslationJobRunResult.Fail(
                "The service account is not authorized for all content items in this translation job.")
            : null;
    }

    private static TranslationJobOutput BuildJobOutput(TranslationJob job, bool statusChanged, bool approved, bool published)
        => new()
        {
            Id = job.Id,
            Key = job.Key,
            Name = job.Name,
            SetKey = job.SetKey,
            SourceCulture = job.SourceCulture?.Name ?? string.Empty,
            TargetCulture = job.TargetCulture?.Name ?? string.Empty,
            Status = TranslationStatusMapper.ToName(job.Status),
            StatusChanged = statusChanged,
            IsComplete = TranslationStatusMapper.IsComplete(job.Status),
            IsError = TranslationStatusMapper.IsError(job.Status),
            NodeCount = job.NodeCount,
            ApprovedNodeCount = job.Nodes?.Count(n => n.Status == NodeStatus.Approved) ?? 0,
            ProviderKey = job.ProviderKey,
            ProviderName = job.ProviderName ?? string.Empty,
            ProviderStatus = job.ProviderStatus,
            Created = job.Created,
            Submitted = job.Submitted,
            Received = job.Received,
            Approved = approved,
            Published = published,
        };

    private static string FormatJobName(string? template, string? contentName, int nodeCount, string targetCulture)
        => string.IsNullOrWhiteSpace(template)
            ? $"{contentName} ({nodeCount}) to {targetCulture}"
            : template;

    // A single large page rather than true pagination - acceptable for the descendant counts
    // an Automate step is expected to run against; revisit if this needs to scale to very
    // large trees.
    private IEnumerable<IContent> GetDescendants(int contentId)
        => _contentService.GetPagedDescendants(contentId, 0, 5000, out _, filter: null, ordering: null);
}
