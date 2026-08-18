using Jumoo.TranslationManager.Core.Models;
using Jumoo.TranslationManager.Core.Providers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;

namespace Jumoo.TranslationManager.Automate.Services;

/// <summary>
/// The complete Translation Manager surface this package uses. Exists because Translation
/// Manager's services are concrete classes with non-virtual methods and therefore cannot be
/// mocked directly - every TM call this package makes goes through here instead, so
/// <see cref="TranslationRunner"/> can be unit tested against a mock of this interface. This
/// is also the one place a future v18 IContent -&gt; IPublishableContentBase drift would land.
/// </summary>
public interface ITranslationManagerFacade
{
    Task<IEnumerable<TranslationSet>> GetSetsByPathAsync(string path);

    Task<IEnumerable<TranslationNode>> CreateNodesAsync(
        TranslationSet set, IContent content, NodeCreationOptions options, IEnumerable<TranslationSetSite> sites);

    Task<TranslationJob?> CreateJobAsync(
        string name, IEnumerable<TranslationNode> nodes, ITranslationProvider provider,
        object options, IUser user, JobOptions jobOptions, string groupId);

    Task<TranslationJob?> LoadJobNodesAsync(TranslationJob job);

    /// <summary>Wraps TM's <c>TranslationJobService.SubmitJob</c>, which has no Async suffix.</summary>
    Task<Attempt<TranslationJob?>> SubmitJobAsync(TranslationJob job);

    Task<TranslationJob?> GetJobByKeyAsync(Guid key);

    Task<TranslationJob?> SaveJobAsync(TranslationJob job);

    /// <summary>Wraps TM's <c>ITranslationAprovalService.ApproveAsync</c> (TM's own spelling).</summary>
    Task<bool> ApproveJobAsync(int jobId, TranslationJobApprovalOptions options);

    ITranslationProvider? GetProvider(Guid key);
}
