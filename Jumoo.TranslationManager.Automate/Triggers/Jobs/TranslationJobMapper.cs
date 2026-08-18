using Jumoo.TranslationManager.Automate.Mapping;
using Jumoo.TranslationManager.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Jumoo.TranslationManager.Automate.Triggers.Jobs;

/// <summary>Maps a Translation Manager job to <see cref="TranslationJobTriggerOutput"/>.</summary>
public sealed class TranslationJobMapper
{
    private readonly IContentService _contentService;

    public TranslationJobMapper(IContentService contentService) => _contentService = contentService;

    public TranslationJobTriggerOutput Map(TranslationJob job, bool publish, Guid userKey, int maxContentKeys)
        => new()
        {
            JobId = job.Id,
            JobKey = job.Key,
            JobName = job.Name,
            SetKey = job.SetKey,
            SourceCulture = job.SourceCulture?.Name ?? string.Empty,
            TargetCulture = job.TargetCulture?.Name ?? string.Empty,
            NodeCount = job.NodeCount,
            ApprovedNodeCount = job.Nodes?.Count(n => n.Status == NodeStatus.Approved) ?? 0,
            Status = TranslationStatusMapper.ToName(job.Status),
            IsError = TranslationStatusMapper.IsError(job.Status),
            ProviderKey = job.ProviderKey,
            ProviderName = job.ProviderName,
            GroupId = job.GroupId,
            UserKey = userKey,
            Publish = publish,
            ContentKeys = MapContentKeys(job, maxContentKeys),
        };

    // Best-effort and cheap: only resolves nodes already loaded on the job at notification
    // time (never forces a LoadJobNodesAsync round-trip from the notification thread), and
    // caps the result - a bulk job can carry thousands of nodes.
    private Guid[] MapContentKeys(TranslationJob job, int max)
    {
        if (job.Nodes is not { Count: > 0 } nodes)
            return [];

        var keys = new List<Guid>();
        foreach (var masterNodeId in nodes.Select(n => n.MasterNodeId).Distinct())
        {
            if (keys.Count >= max) break;

            var content = _contentService.GetById(masterNodeId);
            if (content is not null)
                keys.Add(content.Key);
        }

        return keys.ToArray();
    }
}
