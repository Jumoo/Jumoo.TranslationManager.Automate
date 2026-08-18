using Jumoo.TranslationManager.Core.Models;
using Jumoo.TranslationManager.Core.Providers;
using Jumoo.TranslationManager.Core.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;

namespace Jumoo.TranslationManager.Automate.Services;

/// <inheritdoc cref="ITranslationManagerFacade" />
internal sealed class TranslationManagerFacade : ITranslationManagerFacade
{
    private readonly TranslationSetService _setService;
    private readonly TranslationNodeService _nodeService;
    private readonly TranslationJobService _jobService;
    private readonly ITranslationAprovalService _approvalService;
    private readonly TranslationProviderCollection _providers;

    public TranslationManagerFacade(
        TranslationSetService setService,
        TranslationNodeService nodeService,
        TranslationJobService jobService,
        ITranslationAprovalService approvalService,
        TranslationProviderCollection providers)
    {
        _setService = setService;
        _nodeService = nodeService;
        _jobService = jobService;
        _approvalService = approvalService;
        _providers = providers;
    }

    public Task<IEnumerable<TranslationSet>> GetSetsByPathAsync(string path)
        => _setService.GetSetsByPathAsync(path);

    public Task<IEnumerable<TranslationNode>> CreateNodesAsync(
        TranslationSet set, IContent content, NodeCreationOptions options, IEnumerable<TranslationSetSite> sites)
        => _nodeService.CreateNodesAsync(set, content, options, sites);

    public Task<TranslationJob?> CreateJobAsync(
        string name, IEnumerable<TranslationNode> nodes, ITranslationProvider provider,
        object options, IUser user, JobOptions jobOptions, string groupId)
        => _jobService.CreateJobAsync(name, nodes, provider, options, user, jobOptions, groupId);

    public Task<TranslationJob?> LoadJobNodesAsync(TranslationJob job)
        => _jobService.LoadJobNodesAsync(job);

    public Task<Attempt<TranslationJob?>> SubmitJobAsync(TranslationJob job)
        => _jobService.SubmitJob(job);

    public Task<TranslationJob?> GetJobByKeyAsync(Guid key)
        => _jobService.GetByKeyAsync(key);

    public Task<TranslationJob?> SaveJobAsync(TranslationJob job)
        => _jobService.SaveAsync(job);

    public Task<bool> ApproveJobAsync(int jobId, TranslationJobApprovalOptions options)
        => _approvalService.ApproveAsync(jobId, options);

    public ITranslationProvider? GetProvider(Guid key)
        => _providers.GetProvider(key);
}
